# Web3Script 空投管理系统

## 项目简介

Web3Script 是一个基于 .NET 6.0 WPF 框架开发的桌面端 Web3 空投管理与自动化交互工具。它集成了多链钱包管理、批量任务执行、项目交互、报表统计、代理配置等功能，旨在帮助用户高效管理和参与各类 Web3 项目空投活动。

## 主要功能

- **项目管理**：内置 Web3 项目，自动化执行项目交互任务。
- **任务系统**：批量创建、调度、执行、暂停、恢复和统计任务，支持定时与循环任务。
- **钱包管理**：批量导入、分组、配置和管理钱包，支持主流 EVM 钱包。
- **报表统计**：自动生成任务执行报表，支持导出与分析。
- **代理与网络**：支持SOCKS代理配置，支持机场节点提取后转为本地代理，保障任务执行的网络环境。 

## 目录结构

```
.
├── MainWindow.xaml / .cs      # 主窗口及逻辑
├── App.xaml / .cs             # 应用入口及全局设置
├── Services/                  # 各类核心服务（任务、项目、钱包、报表、代理等）
├── Models/                    # 数据模型（任务、项目、钱包、邮箱等）
├── ViewModels/                # 视图模型（MVVM 支持）
├── Views/                     # 主要功能窗口与对话框
├── ucontrols/                 # 复用型用户控件（项目列表、钱包管理、报表等）
├── Converters/                # WPF 数据转换器
├── Data/                      # 数据存储与加载
├── ConScript/                 # 项目交互脚本（如 Monad、PharosNetwork 等）
├── SSR/                       # 代理工具相关代码 
├── tryResources/              # 图标等资源文件 
├── web3script.csproj          # 项目文件与依赖
└── ...
``` 
## 快速开始

### 环境准备

- 安装 [.NET 6.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- Windows 10 及以上系统

### 编译与运行

```bash
dotnet build
dotnet run
``` 

### 使用说明

- 启动后，主界面左侧为功能导航菜单，右侧为各功能面板。
- 支持项目批量交互、钱包批量管理、任务调度、报表导出、代理配置等。
- 详细功能可参考界面提示与各面板说明。
### 界面展示
 ![image](https://github.com/user-attachments/assets/97c61ad8-9a11-4ff4-8e80-d3ac657aa23f)
![image](https://github.com/user-attachments/assets/72c9e916-bef3-469b-ae57-ee790550d132)
![image](https://github.com/user-attachments/assets/854f4aa1-8417-4274-901e-85ced5442713)

## 进阶用法

- **自定义项目与脚本**：可在 `ConScript/` 目录下扩展交互脚本，或在 `Models/Project.cs` 中添加新项目模板。
- **代理与网络**：支持 SSR/ClashMeta 等多种代理方式，保障任务执行的网络环境。 

## 常见问题

- **多开限制**：程序默认只允许单实例运行，防止数据冲突。
- **数据存储**：任务、项目、钱包等数据默认保存在本地 JSON 文件中。
- **安全性**：请妥善保管钱包私钥，避免泄露。

## 贡献与反馈

- GitHub: [https://github.com/web3scripthome](https://github.com/web3scripthome)
- X(Twitter): [https://x.com/JsscriptHome](https://x.com/JsscriptHome)

--- 
