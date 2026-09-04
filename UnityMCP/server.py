#!/usr/bin/env python3
"""
Unity MCP Server - 简化版
用于连接AI助手和Unity Editor
"""

import asyncio
import json
import sys
from typing import Any, Dict, List, Optional

# MCP协议基础实现
class MCPServer:
    def __init__(self):
        self.tools = {}
        self.resources = {}
        
    def register_tool(self, name: str, description: str, handler):
        self.tools[name] = {
            "name": name,
            "description": description,
            "handler": handler
        }
        
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        method = request.get("method")
        
        if method == "initialize":
            return await self.handle_initialize(request)
        elif method == "tools/list":
            return await self.handle_tools_list(request)
        elif method == "tools/call":
            return await self.handle_tools_call(request)
        else:
            return {"error": f"Unknown method: {method}"}
    
    async def handle_initialize(self, request: Dict[str, Any]) -> Dict[str, Any]:
        return {
            "protocolVersion": "2024-11-05",
            "capabilities": {
                "tools": {}
            },
            "serverInfo": {
                "name": "unity-mcp-server",
                "version": "1.0.0"
            }
        }
    
    async def handle_tools_list(self, request: Dict[str, Any]) -> Dict[str, Any]:
        tools_list = []
        for name, tool in self.tools.items():
            tools_list.append({
                "name": name,
                "description": tool["description"]
            })
        return {"tools": tools_list}
    
    async def handle_tools_call(self, request: Dict[str, Any]) -> Dict[str, Any]:
        params = request.get("params", {})
        tool_name = params.get("name")
        arguments = params.get("arguments", {})
        
        if tool_name in self.tools:
            handler = self.tools[tool_name]["handler"]
            result = await handler(arguments)
            return {"content": [{"type": "text", "text": json.dumps(result)}]}
        else:
            return {"error": f"Tool not found: {tool_name}"}

# Unity连接器
class UnityConnector:
    def __init__(self, host: str = "localhost", port: int = 6400):
        self.host = host
        self.port = port
        self.connected = False
        
    async def connect(self):
        # 这里实现与Unity Editor的连接
        # Unity端需要运行一个监听服务器
        print(f"Connecting to Unity at {self.host}:{self.port}...")
        self.connected = True
        return True
        
    async def send_command(self, command: str, params: Dict[str, Any] = None) -> Dict[str, Any]:
        if not self.connected:
            return {"error": "Not connected to Unity"}
        
        # 模拟发送命令到Unity
        print(f"Sending command: {command}")
        return {"success": True, "command": command}

# 工具处理函数
async def get_unity_info(args: Dict[str, Any]) -> Dict[str, Any]:
    """获取Unity项目信息"""
    return {
        "project_name": "Dapaolou",
        "unity_version": "2022.3.62f3",
        "status": "connected"
    }

async def execute_menu_item(args: Dict[str, Any]) -> Dict[str, Any]:
    """执行Unity菜单项"""
    menu_path = args.get("menu_path", "")
    return {
        "success": True,
        "menu_path": menu_path,
        "message": f"Executed menu: {menu_path}"
    }

async def get_hierarchy(args: Dict[str, Any]) -> Dict[str, Any]:
    """获取场景层级"""
    return {
        "hierarchy": [
            {"name": "GameManager", "type": "GameObject"},
            {"name": "Main Camera", "type": "Camera"},
            {"name": "Directional Light", "type": "Light"}
        ]
    }

async def create_game_object(args: Dict[str, Any]) -> Dict[str, Any]:
    """创建游戏对象"""
    name = args.get("name", "New GameObject")
    return {
        "success": True,
        "name": name,
        "message": f"Created GameObject: {name}"
    }

async def execute_csharp(args: Dict[str, Any]) -> Dict[str, Any]:
    """执行C#代码"""
    code = args.get("code", "")
    return {
        "success": True,
        "message": "Code executed (simulated)",
        "code": code[:100] + "..." if len(code) > 100 else code
    }

# 主函数
async def main():
    server = MCPServer()
    unity = UnityConnector()
    
    # 注册工具
    server.register_tool(
        "get_unity_info",
        "获取Unity项目信息",
        get_unity_info
    )
    
    server.register_tool(
        "execute_menu_item",
        "执行Unity菜单项",
        execute_menu_item
    )
    
    server.register_tool(
        "get_hierarchy",
        "获取场景层级",
        get_hierarchy
    )
    
    server.register_tool(
        "create_game_object",
        "创建游戏对象",
        create_game_object
    )
    
    server.register_tool(
        "execute_csharp",
        "执行C#代码",
        execute_csharp
    )
    
    print("Unity MCP Server started!")
    print("Available tools: get_unity_info, execute_menu_item, get_hierarchy, create_game_object, execute_csharp")
    print("Waiting for connections...")
    
    # 保持服务器运行
    try:
        while True:
            await asyncio.sleep(1)
    except KeyboardInterrupt:
        print("\nServer stopped.")

if __name__ == "__main__":
    asyncio.run(main())
