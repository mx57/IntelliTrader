import json
import logging
from typing import Optional, Dict, Any
import yaml
from magda_agent.memory.procedural import ProceduralMemory
from magda_agent.skills.registry import SkillRegistry

class SkillExporter:
    """
    Utility to package and export successfully created skills from ProceduralMemory
    into a standard format (JSON/YAML) for a skill marketplace, or serialize active registry skills
    into Model Context Protocol (MCP) tool format.
    """
    def __init__(self, procedural_memory: ProceduralMemory) -> None:
        """
        Initialize the SkillExporter.

        Args:
            procedural_memory (ProceduralMemory): The procedural memory database of the agent.
        """
        self.procedural_memory: ProceduralMemory = procedural_memory

    def export_skill(self, name: str, export_format: str = "json", user_id: Optional[int] = None) -> str:
        """
        Exports a skill from ProceduralMemory into JSON or YAML format.

        Args:
            name (str): The name of the skill to export.
            export_format (str): The serialization format, either 'json' or 'yaml'. Defaults to 'json'.
            user_id (Optional[int]): The ID of the user who owns the skill. Defaults to None.

        Returns:
            str: The serialized skill string.
        """
        try:
            results: Any = self.procedural_memory.get_procedure_versions(name=name, user_id=user_id)
            if not results or not results.get("documents"):
                raise ValueError(f"Skill '{name}' not found in procedural memory.")

            # Assume we export the most recent/first match
            document: str = results["documents"][0]
            metadata: Dict[str, Any] = results["metadatas"][0] if results.get("metadatas") else {}

            if "Procedure: " not in document:
                raise ValueError("Skill document does not contain a procedure block.")

            code: str = document.split("Procedure: ")[-1].strip()

            export_data: Dict[str, Any] = {
                "metadata": metadata,
                "parameters": {},  # Future expansion
                "code": code
            }

            if export_format.lower() == "json":
                return json.dumps(export_data, indent=2)
            elif export_format.lower() == "yaml":
                return yaml.dump(export_data, default_flow_style=False)
            else:
                raise ValueError(f"Unsupported format: {export_format}")
        except Exception as e:
            logging.error(f"Failed to export skill '{name}': {e}")
            raise

    def export_skills_as_mcp(self, registry: SkillRegistry, export_format: str = "json") -> str:
        """
        Serializes all registered skills from a SkillRegistry into the standard MCP tool format (JSON or YAML).
        Verifies the compliance of the exported tools against the MCP tool specification before serializing.

        Args:
            registry (SkillRegistry): The registry containing skills to export.
            export_format (str): The format to export to, either 'json' or 'yaml'. Defaults to 'json'.

        Returns:
            str: The serialized string in JSON or YAML representing the MCP tools definition list.
        """
        from magda_agent.integration.mcp_exporter import MCPExporter
        mcp_exporter: MCPExporter = MCPExporter(registry)
        tools: List[Dict[str, Any]] = mcp_exporter.export_tools()

        # Validate compliance with the MCP tool specification
        mcp_exporter.validate_mcp_compliance(tools)

        export_data: Dict[str, Any] = {
            "tools": tools
        }

        if export_format.lower() == "json":
            return json.dumps(export_data, indent=2)
        elif export_format.lower() == "yaml":
            return yaml.dump(export_data, default_flow_style=False)
        else:
            raise ValueError(f"Unsupported format: {export_format}")
