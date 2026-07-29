import sys
import json
import sqlite3
import os
import re
from typing import Dict, Any, List

def get_episodic_memory_count(db_path: str) -> int:
    """
    Retrieves the count of episodic memories stored in the ChromaDB sqlite3 database.

    Args:
        db_path (str): The file path to the chroma.sqlite3 database.

    Returns:
        int: The number of stored events (embeddings), or 0 on failure or if file doesn't exist.
    """
    if not os.path.exists(db_path):
        return 0
    try:
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()
        cursor.execute("SELECT count(*) FROM embeddings;")
        row = cursor.fetchone()
        count = row[0] if row else 0
        conn.close()
        return count
    except Exception:
        return 0

def fetch_drives_from_api() -> Dict[str, float]:
    """
    Attempts to fetch the real-time cognitive drives (energy, boredom) from the running
    FastAPI local server. It uses the authorization token if configured.

    Returns:
        Dict[str, float]: A dictionary containing energy and boredom levels.
    """
    drives: Dict[str, float] = {}
    token: str | None = os.getenv("MAGDA_API_TOKEN")
    if not token:
        return drives

    try:
        import urllib.request
        req = urllib.request.Request(
            "http://localhost:8000/state",
            headers={"Authorization": f"Bearer {token}"}
        )
        with urllib.request.urlopen(req, timeout=0.5) as response:
            res_data = json.loads(response.read().decode())
            state_str: str = res_data.get("state", "")

            energy_match = re.search(r"Energy:\s*([0-9.]+)", state_str)
            boredom_match = re.search(r"Boredom:\s*([0-9.]+)", state_str)
            if energy_match:
                drives["energy"] = float(energy_match.group(1))
            if boredom_match:
                drives["boredom"] = float(boredom_match.group(1))
    except Exception:
        pass
    return drives

def parse_drives_from_logs(logs_path: str) -> Dict[str, float]:
    """
    Parses the latest general log file to extract recorded homeostatic drive levels
    when the API is offline.

    Args:
        logs_path (str): The folder containing the logs.

    Returns:
        Dict[str, float]: A dictionary with extracted energy and boredom levels, or empty dict.
    """
    drives: Dict[str, float] = {}
    if not os.path.exists(logs_path):
        return drives
    try:
        general_logs: List[str] = sorted(
            [os.path.join(logs_path, f) for f in os.listdir(logs_path) if f.endswith("-general.txt")],
            reverse=True
        )
        if not general_logs:
            return drives

        latest_log: str = general_logs[0]
        with open(latest_log, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()

        for line in reversed(lines):
            energy_match = re.search(r"Energy:\s*([0-9.]+)", line)
            boredom_match = re.search(r"Boredom:\s*([0-9.]+)", line)
            if energy_match and boredom_match:
                drives["energy"] = float(energy_match.group(1))
                drives["boredom"] = float(boredom_match.group(1))
                break
    except Exception:
        pass
    return drives

def main() -> None:
    """
    Main function to aggregate and print the agent's current state and drives
    in JSON format to stdout.
    """
    status: Dict[str, Any] = {
        "episodic_memory_count": 0,
        "energy": 0.85,
        "boredom": 0.15
    }

    # 1. Fetch Episodic Memory Count
    db_path = "magda_agent_system/memory_db/chroma.sqlite3"
    status["episodic_memory_count"] = get_episodic_memory_count(db_path)

    # 2. Retrieve Cognitive Drives (API -> Logs -> Fallback)
    api_drives = fetch_drives_from_api()
    if api_drives:
        status.update(api_drives)
    else:
        log_drives = parse_drives_from_logs("log")
        if log_drives:
            status.update(log_drives)

    print(json.dumps(status))

if __name__ == "__main__":
    main()
