import httpx
import json
import uuid
from typing import Any, Dict, Optional
import asyncio
import logging

class MCPClient:
    """
    A client to execute remote MCP server-prefixed tools.
    """
    def __init__(self, timeout: float = 10.0):
        self.timeout = timeout
        self.registered_tools: Dict[str, Any] = {}
        self.registered_servers: Dict[str, str] = {}

    def register_remote_tool(self, tool_name: str, connection_info: Any) -> None:
        """Register a remote MCP tool."""
        self.registered_tools[tool_name] = connection_info
        logging.info(f"Registered remote MCP tool: {tool_name}")

    def register_server(self, name: str, url: str) -> None:
        """Register an entire MCP server."""
        self.registered_servers[name] = url
        logging.info(f"Registered remote MCP server: {name} at {url}")

    def has_tool(self, name: str) -> bool:
        """Check if a remote tool is registered or accessible via a server prefix."""
        if name in self.registered_tools:
            return True
        if ":" in name:
            prefix = name.split(":")[0]
            return prefix in self.registered_servers
        return False

    async def execute_tool(self, name: str, **kwargs) -> Any:
        """
        Execute a remote MCP tool using JSON-RPC over HTTP.
        Supports server-prefixed names (e.g., 'server_name:tool_name').
        """
        url = None
        method = name

        # 1. Check for individually registered tool
        if name in self.registered_tools:
            connection_info = self.registered_tools[name]
            url = connection_info.get("url")
            if not url:
                return f"Error: Remote MCP skill '{name}' has no URL configured."

        # 2. Check for server-prefixed tool
        elif ":" in name:
            prefix, tool_part = name.split(":", 1)
            if prefix in self.registered_servers:
                url = self.registered_servers[prefix]
                method = tool_part
            else:
                return f"Error: MCP server prefix '{prefix}' not found."

        else:
            return f"Error: Remote MCP skill '{name}' not found."

        payload = {
            "jsonrpc": "2.0",
            "method": method,
            "params": kwargs,
            "id": str(uuid.uuid4())
        }

        try:
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                response = await client.post(url, json=payload)
                response.raise_for_status()
                data = response.json()

                if "error" in data:
                    return f"Error from remote MCP server: {data['error']}"
                return data.get("result", "No result returned.")
        except Exception as e:
            logging.error(f"Failed to execute remote MCP tool {name} at {url}: {e}")
            return f"Error executing remote MCP tool {name}: {e}"
