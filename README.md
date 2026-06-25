# Satori消息监听

ClassIsland 插件，通过 Satori 协议（WebSocket）连接到消息服务，接收消息并转发为 ClassIsland 提醒。

## 功能

- 连接到 Satori WebSocket 服务，实时接收消息
- 在主界面组件中显示最近一条消息（群消息显示"发件人(群名):内容"，私聊显示"发件人：内容"）
- 收到消息时通过 ClassIsland 通知系统弹窗提醒
- 支持自定义 WebSocket 地址
- 支持设置不提醒的群聊和发件人（黑名单关键词过滤）

## 使用方法

1. 在 ClassIsland 中加载本插件
2. 打开设置 → Satori消息
3. 填入 Satori 服务的 WebSocket 地址（如 `ws://localhost:5140/events`）
4. 根据需要配置不提醒的群聊和发件人
5. 在主界面添加「Satori消息」组件即可查看最新消息

## 构建

在项目根目录执行：

```bash
dotnet publish -c Release -o publish
```

构建产物位于 `publish/` 目录。

## 许可

MIT License
