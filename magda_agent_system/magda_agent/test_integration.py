import asyncio
import logging
import sys
import pytest
from magda_agent.llm_client import LLMClient
from magda_agent.emotions.engine import EmotionalEngine
from magda_agent.memory.storage import MemorySystem
from magda_agent.skills import initialize_skills
from magda_agent.consciousness.core import Consciousness
from magda_agent.subconsciousness.reflection import Subconsciousness
from magda_agent.memory.long_term import LongTermMemory

@pytest.mark.asyncio
async def test_integration():
    logging.basicConfig(level=logging.INFO)
    logging.info("Starting Magda Agent Integration Test...")

    # Mocking LLM client to avoid API calls and costs during tests
    class MockLLMClient(LLMClient):
        async def chat_completion(self, messages, temperature=0.7):
            return "This is a mock response from Magda."

    llm = MockLLMClient(api_key="test_key")
    emotions = EmotionalEngine()
    memory = MemorySystem()
    skills = initialize_skills()

    # Use EphemeralClient for tests
    long_term_memory = LongTermMemory(persist_directory=":memory:")

    consciousness = Consciousness(llm, emotions, memory, skills, long_term_memory=long_term_memory)
    subconsciousness = Subconsciousness(llm, emotions, memory, interval=1)

    # 1. Test Consciousness processing
    logging.info("Testing Consciousness...")
    response = await consciousness.process_input("Hello, how are you?")
    assert "mock response" in response.lower()
    assert len(memory.short_term) > 0
    logging.info("Consciousness test passed.")

    # 2. Test Emotional State
    logging.info(f"Current Emotion: {emotions.get_emotion_label()}")
    assert emotions.state.arousal != 0.0

    # 3. Test Subconsciousness reflection
    logging.info("Testing Subconsciousness...")
    await subconsciousness.reflect()
    assert any("Subconscious reflection" in m.content for m in memory.short_term)
    logging.info("Subconsciousness test passed.")

    # 4. Test Skills Registry
    logging.info("Testing Skills...")
    result = skills.execute_skill("programmer", code="print('Hello World')")
    assert "Hello World" in result
    logging.info("Skills test passed.")

    logging.info("Integration Test Successful!")


@pytest.mark.asyncio
async def test_mcp_exporter():
    logging.info("Starting MCP Exporter Integration and Compliance Test...")

    from magda_agent.skills import initialize_skills
    from magda_agent.integration.mcp_exporter import MCPExporter
    from magda_agent.skills.exporter import SkillExporter
    from magda_agent.memory.procedural import ProceduralMemory

    skills = initialize_skills()
    exporter = MCPExporter(skills)

    # 1. Test tools listing and format
    tools = exporter.export_tools()
    assert len(tools) > 0, "No tools exported!"

    # Verify all tools comply with the MCP tool specification
    assert exporter.validate_mcp_compliance(tools) is True

    # 2. Test JSON-RPC tools/list
    list_req = {
        "jsonrpc": "2.0",
        "method": "tools/list",
        "id": "test-list-1"
    }
    list_resp = await exporter.handle_rpc_request(list_req)
    assert list_resp["jsonrpc"] == "2.0"
    assert list_resp["id"] == "test-list-1"
    assert "result" in list_resp
    assert "tools" in list_resp["result"]
    assert len(list_resp["result"]["tools"]) == len(tools)

    # 3. Test JSON-RPC tools/call for a direct valid call
    def greet(person_name: str) -> str:
        return f"Hello, {person_name}!"

    skills.register_skill("greet_tool", greet, "Greets a person by name. Input: 'person_name' string.")

    call_req = {
        "jsonrpc": "2.0",
        "method": "tools/call",
        "params": {
            "name": "greet_tool",
            "arguments": {
                "person_name": "Jules"
            }
        },
        "id": "test-call-1"
    }
    call_resp = await exporter.handle_rpc_request(call_req)
    assert call_resp["jsonrpc"] == "2.0"
    assert call_resp["id"] == "test-call-1"
    assert "result" in call_resp
    assert call_resp["result"]["isError"] is False
    assert "Hello, Jules!" in call_resp["result"]["content"][0]["text"]

    # 4. Test compliance validation with non-compliant data
    bad_tool_missing_name = {
        "description": "Missing name",
        "inputSchema": {"type": "object"}
    }
    with pytest.raises(ValueError, match="is missing required field 'name'"):
        exporter.validate_mcp_compliance([bad_tool_missing_name])

    bad_tool_invalid_name = {
        "name": "bad tool name containing spaces",
        "description": "Invalid name pattern",
        "inputSchema": {"type": "object"}
    }
    with pytest.raises(ValueError, match="does not match MCP regex pattern"):
        exporter.validate_mcp_compliance([bad_tool_invalid_name])

    bad_tool_wrong_schema_type = {
        "name": "bad_schema_type",
        "description": "Wrong schema type",
        "inputSchema": {"type": "array"}
    }
    with pytest.raises(ValueError, match="inputSchema must have type 'object'"):
        exporter.validate_mcp_compliance([bad_tool_wrong_schema_type])

    # 5. Test SkillExporter serialize capability
    procedural_memory = ProceduralMemory()
    skill_exporter = SkillExporter(procedural_memory)

    json_mcp_str = skill_exporter.export_skills_as_mcp(skills, export_format="json")
    assert '"tools":' in json_mcp_str

    yaml_mcp_str = skill_exporter.export_skills_as_mcp(skills, export_format="yaml")
    assert 'tools:' in yaml_mcp_str

    logging.info("MCP Exporter and Compliance Tests passed!")


if __name__ == "__main__":
    asyncio.run(test_integration())
    asyncio.run(test_mcp_exporter())
