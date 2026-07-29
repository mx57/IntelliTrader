import sys
import json
import sqlite3
import os
from datetime import datetime, timedelta
from typing import Dict, Any, List

def get_parameter_adjustments(db_path: str) -> List[Dict[str, Any]]:
    """
    Retrieves the list of parameter adjustments from ChromaDB sqlite3 database.

    Args:
        db_path (str): The file path to the chroma.sqlite3 database.

    Returns:
        List[Dict[str, Any]]: A list of dictionaries representing parameter adjustments.
    """
    adjustments: List[Dict[str, Any]] = []
    if not os.path.exists(db_path):
        return adjustments

    try:
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()

        # Query to join embeddings and their metadata for parameter adjustments
        query = """
        SELECT
            e.id,
            e.created_at,
            (SELECT string_value FROM embedding_metadata WHERE id = e.id AND key = 'pair') AS pair,
            (SELECT float_value FROM embedding_metadata WHERE id = e.id AND key = 'new_BuyTrailing') AS new_BuyTrailing,
            (SELECT float_value FROM embedding_metadata WHERE id = e.id AND key = 'new_SellMargin') AS new_SellMargin,
            (SELECT float_value FROM embedding_metadata WHERE id = e.id AND key = 'old_BuyTrailing') AS old_BuyTrailing,
            (SELECT float_value FROM embedding_metadata WHERE id = e.id AND key = 'old_SellMargin') AS old_SellMargin
        FROM embeddings e
        WHERE e.id IN (
            SELECT id FROM embedding_metadata WHERE key = 'type' AND string_value = 'parameter_adjustment'
        )
        ORDER BY e.created_at ASC;
        """

        cursor.execute(query)
        rows = cursor.fetchall()
        for row in rows:
            created_at_str = row[1]
            # ChromaDB timestamps are usually ISO or standard datetime strings
            # We will parse or format it
            try:
                dt = datetime.fromisoformat(created_at_str.replace("Z", "+00:00"))
                formatted_date = dt.strftime("%Y-%m-%d %H:%M:%S")
            except Exception:
                formatted_date = created_at_str

            adjustments.append({
                "id": row[0],
                "timestamp": formatted_date,
                "pair": row[2] or "BTC/USDT",
                "new_BuyTrailing": row[3],
                "new_SellMargin": row[4],
                "old_BuyTrailing": row[5],
                "old_SellMargin": row[6]
            })

        conn.close()
    except Exception as e:
        # Fail gracefully
        pass

    return adjustments

def generate_mock_data() -> List[Dict[str, Any]]:
    """
    Generates fallback/mock parameter adjustment data points for visual completeness when
    the database has no real adjustment entries yet.

    Returns:
        List[Dict[str, Any]]: A list of simulated parameter adjustments over the last 10 days.
    """
    mock_data: List[Dict[str, Any]] = []
    base_time = datetime.now() - timedelta(days=10)

    # Pre-defined realistic drift path
    drift_steps = [
        (0.150, 2.000, 0.150, 2.000), # initial
        (0.145, 2.100, 0.150, 2.000),
        (0.130, 2.250, 0.145, 2.100),
        (0.155, 2.150, 0.130, 2.250),
        (0.160, 1.950, 0.155, 2.150),
        (0.140, 2.300, 0.160, 1.950),
        (0.125, 2.450, 0.140, 2.300),
        (0.110, 2.600, 0.125, 2.450),
        (0.115, 2.500, 0.110, 2.600),
        (0.120, 2.550, 0.115, 2.500)
    ]

    for idx, step in enumerate(drift_steps):
        timestamp = (base_time + timedelta(days=idx)).strftime("%Y-%m-%d %H:%M:%S")
        mock_data.append({
            "id": idx + 1000,
            "timestamp": timestamp,
            "pair": "BTC/USDT",
            "new_BuyTrailing": step[0],
            "new_SellMargin": step[1],
            "old_BuyTrailing": step[2],
            "old_SellMargin": step[3]
        })

    return mock_data

def main() -> None:
    """
    Main execution entrypoint. Aggregates parameter drift data from SQLite or generates
    mock fallback data if empty, then prints JSON output to stdout.
    """
    db_path = "magda_agent_system/memory_db/chroma.sqlite3"
    adjustments = get_parameter_adjustments(db_path)

    is_mock = False
    if not adjustments:
        adjustments = generate_mock_data()
        is_mock = True

    response = {
        "is_mock": is_mock,
        "adjustments": adjustments
    }

    print(json.dumps(response))

if __name__ == "__main__":
    main()
