from typing import Dict, Any, List
import uuid
import re
from magda_agent.skills.registry import SkillRegistry
from magda_agent.skills.mcp_export import MagdaMCPAdapter

class MCPExporter:
    """
    Exports Magda skills as MCP-compatible JSON-RPC tools and acts as a server-side bridge.
    Provides validation to confirm compliance with the Model Context Protocol (MCP) tool specification.
    """
    def __init__(self, registry: SkillRegistry) -> None:
        """
        Initialize the MCPExporter with a SkillRegistry.

        Args:
            registry (SkillRegistry): The registry holding the available skills.
        """
        self.registry: SkillRegistry = registry
        self.adapter: MagdaMCPAdapter = MagdaMCPAdapter(registry)

    def export_tools(self) -> List[Dict[str, Any]]:
        """
        Returns a list of exported MCP tools from the registered skills.

        Returns:
            List[Dict[str, Any]]: A list of dictionaries matching the MCP tool definition.
        """
        return self.adapter.list_tools()

    def validate_mcp_compliance(self, tools_list: List[Dict[str, Any]]) -> bool:
        """
        Validates whether a list of tool definitions conforms to the Model Context Protocol (MCP) tool specification.

        Args:
            tools_list (List[Dict[str, Any]]): A list of dictionaries representing MCP tool definitions.

        Returns:
            bool: True if all tools comply, False otherwise (or raises ValueError/TypeError with details).
        """
        for tool in tools_list:
            if not isinstance(tool, dict):
                raise TypeError("Tool definition must be a dictionary.")

            # Validate 'name'
            if "name" not in tool:
                raise ValueError("Tool is missing required field 'name'.")
            name: Any = tool["name"]
            if not isinstance(name, str):
                raise TypeError(f"Tool name must be a string, got {type(name)}.")
            if not re.match(r"^[a-zA-Z0-9_-]{1,64}$", name):
                raise ValueError(f"Tool name '{name}' does not match MCP regex pattern ^[a-zA-Z0-9_-]{{1,64}}$")

            # Validate 'description'
            if "description" not in tool:
                raise ValueError(f"Tool '{name}' is missing required field 'description'.")
            description: Any = tool["description"]
            if not isinstance(description, str):
                raise TypeError(f"Tool '{name}' description must be a string, got {type(description)}.")

            # Validate 'inputSchema'
            if "inputSchema" not in tool:
                raise ValueError(f"Tool '{name}' is missing required field 'inputSchema'.")
            schema: Any = tool["inputSchema"]
            if not isinstance(schema, dict):
                raise TypeError(f"Tool '{name}' inputSchema must be a dictionary, got {type(schema)}.")
            if schema.get("type") != "object":
                raise ValueError(f"Tool '{name}' inputSchema must have type 'object', got '{schema.get('type')}'")

            # Validate properties inside inputSchema
            properties: Any = schema.get("properties", {})
            if not isinstance(properties, dict):
                raise TypeError(f"Tool '{name}' properties must be a dictionary.")
            for prop_name, prop_val in properties.items():
                if not isinstance(prop_val, dict):
                    raise TypeError(f"Property '{prop_name}' in tool '{name}' must be defined as a dictionary.")
                if "type" not in prop_val:
                    raise ValueError(f"Property '{prop_name}' in tool '{name}' is missing required 'type' field.")

            # Validate required fields list
            required: Any = schema.get("required", [])
            if not isinstance(required, list):
                raise TypeError(f"Tool '{name}' required fields must be a list, got {type(required)}.")
            for req_field in required:
                if not isinstance(req_field, str):
                    raise TypeError(f"Required field names must be strings, got {type(req_field)}.")
                if req_field not in properties:
                    raise ValueError(f"Required field '{req_field}' in tool '{name}' is not defined in properties.")

        return True

    async def handle_rpc_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """
        Handles an incoming JSON-RPC 2.0 request for a tool execution or tool listing.
        Supports standard MCP methods 'tools/list' and 'tools/call', in addition to direct skill invocation.

        Args:
            request (Dict[str, Any]): A dictionary representing a JSON-RPC 2.0 request.

        Returns:
            Dict[str, Any]: A dictionary representing a JSON-RPC 2.0 response.
        """
        req_id: Any = request.get("id", str(uuid.uuid4()))
        method: Any = request.get("method")
        params: Any = request.get("params", {})

        if request.get("jsonrpc") != "2.0":
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32600, "message": "Invalid Request"}
            }

        if not method:
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32601, "message": "Method not found"}
            }

        # Handle standard MCP tools/list method
        if method == "tools/list":
            try:
                tools: List[Dict[str, Any]] = self.export_tools()
                # Ensure the exported list is fully compliant
                self.validate_mcp_compliance(tools)
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "result": {
                        "tools": tools
                    }
                }
            except Exception as e:
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32603, "message": f"Internal error listing tools: {e}"}
                }

        # Handle standard MCP tools/call method
        if method == "tools/call":
            if not isinstance(params, dict):
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32602, "message": "Invalid params: must be a dictionary containing 'name' and optional 'arguments'"}
                }
            tool_name: Any = params.get("name")
            if not tool_name:
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32602, "message": "Missing required param 'name' for tools/call"}
                }
            if not isinstance(tool_name, str):
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32602, "message": "Param 'name' must be a string"}
                }
            if not self.registry.has_skill(tool_name):
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32601, "message": f"Tool '{tool_name}' not found"}
                }
            arguments: Any = params.get("arguments", {})
            if not isinstance(arguments, dict):
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32602, "message": "Param 'arguments' must be a dictionary"}
                }

            adapter_result: Dict[str, Any] = await self.adapter.call_tool_async(tool_name, arguments)
            if adapter_result.get("isError"):
                error_msg: Any = adapter_result.get("content", [{"text": "Unknown error"}])[0].get("text")
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32000, "message": error_msg}
                }
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": adapter_result
            }

        # Direct-skill-call backward compatibility
        if not self.registry.has_skill(method):
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32601, "message": f"Method '{method}' not found"}
            }

        if not isinstance(params, dict):
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32602, "message": "Invalid params for direct call"}
            }

        direct_adapter_result: Dict[str, Any] = await self.adapter.call_tool_async(method, params)

        if direct_adapter_result.get("isError"):
            direct_error_msg: Any = direct_adapter_result.get("content", [{"text": "Unknown error"}])[0].get("text")
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32000, "message": direct_error_msg}
            }

        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": direct_adapter_result
        }
