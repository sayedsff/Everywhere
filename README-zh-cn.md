<a id="readme-top"></a>

<div align="center">
  <img src="https://raw.githubusercontent.com/DearVa/Everywhere/refs/heads/main/img/banner.png" alt="Banner"/>

  <h1>

  随时随地，智能相伴 - `Everywhere`

  </h1>

  <p align="center">
    <br />
    <h2>🚧 注意：本项目正在积极开发，尚无法投入使用 🚧</h2>
    <br/>
    <a href="https://github.com/DearVa/Everywhere"><strong>（开发中）查看文档 »</strong></a>
    <br />
    <br />
    <a href="https://github.com/DearVa/Everywhere">查看演示</a>
    &middot;
    <a href="https://github.com/DearVa/Everywhere/issues/new?labels=bug&template=bug-report.md">报告错误</a>
    &middot;
    <a href="https://github.com/DearVa/Everywhere/issues/new?labels=enhancement&template=feature-request.md">功能请求</a>
  </p>
</div>

---

<a href="https://github.com/DearVa/Everywhere">Go to English Version »</a>

[![贡献者][contributors-shield]][contributors-url]
[![Fork 数][forks-shield]][forks-url]
[![Star 数][stars-shield]][stars-url]
[![问题数][issues-shield]][issues-url]
[![MIT 许可证][license-shield]][license-url]

## 关于 Everywhere

**Everywhere** 是一款基于 .NET 和 Avalonia 构建的跨平台 AI 助手应用。它能在任意桌面应用中直接提供上下文感知的多模态 AI 支持——无论你是在浏览网页、撰写文档、进行设计，还是编写代码。

### ✨ 主要特性

* **通用输入助手** – 在任意文本框中唤起助手，生成、润色、翻译或总结内容，无需切换应用程序。
* **屏幕感知** – 捕获并分析当前窗口或所选区域，提供上下文相关的建议或摘要。
* **MCP 集成** – 利用 Model Context Protocol 连接自定义工具和数据源，进行更深层次的自动化。
* **多模态理解** – 支持文本、图像、截图处理，执行代码解析、界面分析、视觉问答等任务。
* **自由选择LLM** – 自由选择云端 LLM（如 ChatGPT、DeepSeek）或本地模型（如 Ollama、LM Studio），确保数据尽在掌握。

### 🛠 构建工具

[![.NET Core][.NET Core]][.NET-url][![Avalonia][Avalonia]][Avalonia-url]

---

## 📋 系统要求

| 平台      | 最低版本    |
|---------|---------|
| Windows | 10.0.19041.0 |
| macOS   | *计划支持*  |

---

## 🚀 快速开始

### 安装

> **注意**：目前尚无发行版，仅支持从源码构建。

---

## ✈️ 路线图

- [x] 基于屏幕的助手
- [x] 网页搜索
- [ ] 推理能力
- [ ] MCP 工具集成
- [ ] 语音输入
- [ ] RAG 和知识库

---

## 🤝 贡献方式

欢迎提交 Issue、提出功能建议或贡献代码！请参考 [CONTRIBUTING.md](CONTRIBUTING.md) 查看具体指南。

### 主要贡献者：

<a href="https://github.com/DearVa/Everywhere/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=DearVa/Everywhere" alt="贡献者图示" />
</a>

---

## 📄 许可证

本项目基于 Apache 2.0 许可证发布，详情请查阅 [`LICENSE`](LICENSE)。

### 第三方许可证

- **shad-ui** - [MIT License](https://github.com/accntech/shad-ui/blob/main/LICENSE)
    - UI 样式和组件
    - Source repo: https://github.com/accntech/shad-ui
- **Lucide.Avalonia** - [MIT License](https://github.com/dme-compunet/Lucide.Avalonia/blob/master/LICENSE)
    - 图标
    - Source repo: https://github.com/dme-compunet/Lucide.Avalonia
- **HotAvalonia** - [MIT License](https://github.com/Kira-NT/HotAvalonia/blob/master/LICENSE.md)
    - 热重载支持
    - Source repo: https://github.com/Kira-NT/HotAvalonia
- **CommunityToolkit.Mvvm** - [MIT License](https://github.com/CommunityToolkit/dotnet/blob/main/License.md)
    - MVVM 框架
    - Source repo: https://github.com/CommunityToolkit/dotnet
- **ObservableCollections** - [MIT License](https://github.com/Cysharp/ObservableCollections/blob/master/LICENSE)
    - Observable collections for MVVM
    - Source repo: https://github.com/Cysharp/ObservableCollections
- **Zlinq** - [MIT License](https://github.com/Cysharp/ZLinq/blob/master/LICENSE)
    - 零分配 LINQ
    - Source repo: https://github.com/Cysharp/ZLinq
- **FlaUI** - [MIT License](https://github.com/FlaUI/FlaUI/blob/master/LICENSE.txt)
    - 屏幕感知和 UI 自动化
    - Source repo: https://github.com/FlaUI/FlaUI
- **CsWin32** - [MIT License](https://github.com/microsoft/CsWin32/blob/main/LICENSE)
    - Win32 API 自动生成
    - Source repo: https://github.com/microsoft/CsWin32
- **WritableJsonConfiguration** - [MIT License](https://github.com/Kibnet/WritableJsonConfiguration/blob/master/LICENSE)
    - 设置存储
    - Source repo: https://github.com/Kibnet/WritableJsonConfiguration
- **semantic-kernel** - [MIT License](https://github.com/microsoft/semantic-kernel/blob/main/LICENSE)
    - 语义内核和插件系统
    - Source repo: https://github.com/microsoft/semantic-kernel
- **Sep** - [MIT License](https://github.com/nietras/Sep/blob/main/LICENSE)
    - TSV 读取器，用于国际化
    - Source repo: https://github.com/nietras/Sep
- **markdig** - [BSD-2-Clause License](https://github.com/xoofx/markdig/blob/master/license.txt)
    - Markdown 解析器，用于 Everywhere.Markdown 渲染
    - Source repo: https://github.com/xoofx/markdig
- **AsyncImageLoader.Avalonia** - [MIT License](https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia/blob/master/LICENSE)
    - 异步图像加载器
    - Source repo: https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia

---

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- MARKDOWN LINKS & IMAGES -->
[contributors-shield]: https://img.shields.io/github/contributors/DearVa/Everywhere.svg?style=for-the-badge
[contributors-url]: https://github.com/DearVa/Everywhere/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/DearVa/Everywhere.svg?style=for-the-badge
[forks-url]: https://github.com/DearVa/Everywhere/network/members
[stars-shield]: https://img.shields.io/github/stars/DearVa/Everywhere.svg?style=for-the-badge
[stars-url]: https://github.com/DearVa/Everywhere/stargazers
[issues-shield]: https://img.shields.io/github/issues/DearVa/Everywhere.svg?style=for-the-badge
[issues-url]: https://github.com/DearVa/Everywhere/issues
[license-shield]: https://img.shields.io/github/license/DearVa/Everywhere.svg?style=for-the-badge
[license-url]: https://github.com/DearVa/Everywhere/blob/master/LICENSE.txt
[.NET Core]: https://img.shields.io/badge/.NET_Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white
[.NET-url]: https://dotnet.microsoft.com/
[Avalonia]: https://img.shields.io/badge/Avalonia-1c2e5f?style=for-the-badge&logo=data:image/svg%2bxml;base64,PHN2ZyB3aWR0aD0iODYiIGhlaWdodD0iODYiIHZpZXdCb3g9IjAgMCA4NiA4NiIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPGcgY2xpcC1wYXRoPSJ1cmwoI2NsaXAwXzU5OV8xMTA3KSI+CjxwYXRoIGQ9Ik03NC44NTM1IDg1LjgyMzFDNzUuMDI2MyA4NS44MjMxIDc1LjE5NTQgODUuODIzMSA3NS4zNjc5IDg1LjgyMzFDODAuNzM0NyA4NS44MjMxIDg1LjE0MzkgODEuODAyNyA4NS43NjE0IDc2LjYwMTlMODUuODM1NyA0MS43NjA0Qzg1LjIyNTUgMTguNTkzMSA2Ni4yNTM3IDAgNDIuOTM5MyAwQzE5LjIzOTkgMCAwLjAyNzcxIDE5LjIxMjIgMC4wMjc3MSA0Mi45MTE2QzAuMDI3NzEgNjYuMzU3MyAxOC44MzA5IDg1LjQxOCA0Mi4xOCA4NS44MjMxSDc0Ljg1MzVaIiBmaWxsPSIjRjlGOUZCIi8+CjxwYXRoIGZpbGwtcnVsZT0iZXZlbm9kZCIgY2xpcC1ydWxlPSJldmVub2RkIiBkPSJNNDMuMDU4NSAxNC42MTQzQzI5LjU1MTMgMTQuNjE0MyAxOC4yNTU1IDI0LjA4MiAxNS40NDU0IDM2Ljc0MzJDMTguMTM1NyAzNy40OTc1IDIwLjEwODcgMzkuOTY3OSAyMC4xMDg3IDQyLjg5OTJDMjAuMTA4NyA0NS44MzA1IDE4LjEzNTcgNDguMzAxIDE1LjQ0NTQgNDkuMDU1MkMxOC4yNTU1IDYxLjcxNjQgMjkuNTUxMyA3MS4xODQyIDQzLjA1ODUgNzEuMTg0MkM0Ny45NzU0IDcxLjE4NDIgNTIuNTk5MyA2OS45Mjk2IDU2LjYyNzYgNjcuNzIzVjcwLjk5MjZINzEuMzQzNVY0NC4wNzE2QzcxLjM1NjkgNDMuNzEzOCA3MS4zNDM1IDQzLjI2MDMgNzEuMzQzNSA0Mi44OTkyQzcxLjM0MzUgMjcuMjc3OSA1OC42Nzk5IDE0LjYxNDMgNDMuMDU4NSAxNC42MTQzWk0yOS41MDk2IDQyLjg5OTJDMjkuNTA5NiAzNS40MTY0IDM1LjU3NTcgMjkuMzUwMyA0My4wNTg1IDI5LjM1MDNDNTAuNTQxNCAyOS4zNTAzIDU2LjYwNzQgMzUuNDE2NCA1Ni42MDc0IDQyLjg5OTJDNTYuNjA3NCA1MC4zODIxIDUwLjU0MTQgNTYuNDQ4MSA0My4wNTg1IDU2LjQ0ODFDMzUuNTc1NyA1Ni40NDgxIDI5LjUwOTYgNTAuMzgyMSAyOS41MDk2IDQyLjg5OTJaIiBmaWxsPSIjMTYxQzJEIi8+CjxwYXRoIGQ9Ik0xOC4xMDUgNDIuODgwNUMxOC4xMDUgNDUuMzgwMyAxNi4wNzg1IDQ3LjQwNjggMTMuNTc4NyA0Ny40MDY4QzExLjA3ODkgNDcuNDA2OCA5LjA1MjM3IDQ1LjM4MDMgOS4wNTIzNyA0Mi44ODA1QzkuMDUyMzcgNDAuMzgwNyAxMS4wNzg5IDM4LjM1NDIgMTMuNTc4NyAzOC4zNTQyQzE2LjA3ODUgMzguMzU0MiAxOC4xMDUgNDAuMzgwNyAxOC4xMDUgNDIuODgwNVoiIGZpbGw9IiMxNjFDMkQiLz4KPC9nPgo8ZGVmcz4KPGNsaXBQYXRoIGlkPSJjbGlwMF81OTlfMTEwNyI+CjxyZWN0IHdpZHRoPSI4NiIgaGVpZ2h0PSI4NiIgZmlsbD0id2hpdGUiLz4KPC9jbGlwUGF0aD4KPC9kZWZzPgo8L3N2Zz4K
[Avalonia-url]: https://avaloniaui.net/
