# 🖍️ 蜡笔小新桌面消息通知

一个轻量级的 Windows 桌面通知应用。通过 API 发送文字或图片，蜡笔小新会拉着横幅从屏幕右侧跑到左侧！

## ✨ 功能特点

- 🏃 **桌面动画** - 蜡笔小新拉着横幅跑过桌面（像素风格）
- 🎯 **横幅跟随** - 横幅跟着小新从右侧拉入，有绳索连接
- 📝 **文字消息** - 发送文字显示在横幅上
- 🖼️ **图片消息** - 发送图片显示在横幅上
- 🌐 **Web 界面** - 浏览器打开即可使用
- 📡 **HTTP API** - 可通过程序调用
- 💻 **原生 WPF** - Windows 原生，资源占用低
- 📦 **单文件发布** - 可打包成单个 exe
- 🔔 **系统托盘** - 托盘图标右键可退出程序

## 🚀 快速开始

### 开发环境运行

```bash
# 进入项目目录
cd CrayonShinchanNotification

# 运行
dotnet run
```

### 发布为单文件 exe

```bash
# 进入项目目录
cd CrayonShinchanNotification

# 发布（单文件，包含运行时）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 生成的文件在 bin/Release/net9.0-windows/win-x64/publish/CrayonShinchanNotification.exe
```

### 远程访问（给女朋友用）

程序启动后监听在 `0.0.0.0:8000`，支持从外部访问。

#### 方法一：ngrok 隧道（最简单）

1. 在女朋友电脑上安装 [ngrok](https://ngrok.com/)
2. 启动程序后，运行：`ngrok http 8000`
3. 复制公网地址（如 `https://xxxx.ngrok-free.app`）发给你
4. 你在浏览器打开该地址，输入 API Key 即可发送消息

#### 方法二：Cloudflare Tunnel（免费稳定）

1. 安装 [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/)
2. 运行：`cloudflared tunnel --url http://localhost:8000`
3. 复制公网地址（如 `https://xxx.trycloudflare.com`）

#### 方法三：路由器端口转发

1. 在路由器设置中将外网 8000 端口转发到女朋友电脑的内网 IP:8000
2. 通过你的公网 IP:8000 访问

## 📖 使用方法

### 1. 启动应用

运行程序后，控制台会显示：
```
=========================================
  Crayon Shin-chan Notification
  API:  http://127.0.0.1:8000
  Web:  http://127.0.0.1:8000/
  托盘图标右键可以退出程序
=========================================
```

### 2. 打开 Web 界面

浏览器访问 http://127.0.0.1:8000/

- **文字标签**：输入文字，按 Enter 或点击发送
- **图片标签**：选择图片、拖拽图片、或 Ctrl+V 粘贴图片

### 3. 关闭程序

- **方法一**：右键点击系统托盘的红色 "S" 图标 → 选择 "退出"
- **方法二**：双击托盘图标打开 Web 界面

### 4. 通过 API 调用

> ⚠️ 发送消息的 API 需要携带 API Key（通过环境变量 `SHINCHAN_API_KEY` 设置，默认值 `shinchan2024`）

#### 发送文字

```bash
curl -X POST http://127.0.0.1:8000/api/send \
     -H "Content-Type: application/json" \
     -H "X-Api-Key: shinchan2024" \
     -d '{"type":"text","content":"你好，蜡笔小新！"}'
```

#### 发送图片（Base64）

```bash
curl -X POST http://127.0.0.1:8000/api/send \
     -H "Content-Type: application/json" \
     -H "X-Api-Key: shinchan2024" \
     -d '{"type":"image","content":"iVBORw0KGgo..."}'
```

#### 上传图片文件

```bash
curl -X POST http://127.0.0.1:8000/api/send-image \
     -H "X-Api-Key: shinchan2024" \
     -F "file=@image.png"
```

## 📁 项目结构

```
Crayon Shin-chan message notification/
├── CrayonShinchanNotification/
│   ├── CrayonShinchanNotification.csproj  # 项目文件
│   ├── App.xaml / App.xaml.cs             # 应用入口 + API 服务器
│   ├── MainWindow.xaml / MainWindow.xaml.cs # 透明覆盖窗口 + 动画引擎
│   ├── MessageData.cs                     # 消息数据模型
│   └── wwwroot/
│       └── index.html                     # Web 前端界面
├── test_api.bat                           # API 测试脚本
├── .gitignore
└── README.md
```

## 🎨 技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| 桌面覆盖层 | WPF | 透明窗口，硬件加速渲染 |
| API 服务器 | ASP.NET Core Minimal API | 轻量高性能 HTTP 服务 |
| 前端 | 原生 HTML/CSS/JS | 零依赖，即开即用 |
| 动画 | WPF Canvas + DispatcherTimer | 60fps 流畅动画 |
| 精灵图 | 程序化生成 | 像素风格蜡笔小新 |

## 🖼️ 自定义 GIF

当前使用 `shinchan.gif` 作为角色动画。你可以替换为自己的 GIF 文件：

1. 准备一个透明背景的 GIF 文件
2. 命名为 `shinchan.gif`
3. 放到项目目录 `CrayonShinchanNotification/` 下
4. 重新编译或直接复制到输出目录

## ⚙️ 配置

### 端口修改

修改 `App.xaml.cs` 中的端口号：

```csharp
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://0.0.0.0:8000");
```

### API Key

通过环境变量设置 API Key（用于保护发送消息接口）：

```bash
# Windows PowerShell
$env:SHINCHAN_API_KEY="your_secret_key"

# Windows CMD
set SHINCHAN_API_KEY=your_secret_key
```

默认值：`shinchan2024`

### 动画时长

修改 `MainWindow.xaml.cs` 中的常量：

```csharp
private const double AnimDuration = 8.0; // 秒
```

### 角色大小

```csharp
private const int CharScale = 4; // 像素缩放倍数
```

## 🔌 API 文档

启动应用后访问 http://127.0.0.1:8000/ 查看 API 说明。

### POST /api/send

发送文字或图片消息。需要 `X-Api-Key` 请求头。

**请求头：**
```
X-Api-Key: shinchan2024
Content-Type: application/json
```

**请求体：**
```json
{
  "type": "text",      // "text" 或 "image"
  "content": "消息内容"  // 文字内容或 Base64 编码的图片
}
```

**响应：**
```json
{
  "status": "ok",
  "message": "Animation triggered"
}
```

### POST /api/send-image

上传图片文件。需要 `X-Api-Key` 请求头。

**请求头：**
```
X-Api-Key: shinchan2024
```

**请求：** `multipart/form-data`，字段名 `file`

**限制：** 最大 5MB

### GET /health

健康检查。

**响应：**
```json
{
  "status": "running"
}
```

## 🛠️ 开发要求

- Windows 10/11
- .NET 9.0 SDK
- Visual Studio 2022 或 VS Code + C# 扩展

## 📄 License

MIT License
