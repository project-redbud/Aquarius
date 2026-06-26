# 🫧 Aquarius — 漂流瓶 / Message in a Bottle

> 匿名社交 Web 应用，支持投瓶、捞瓶、评论、点赞、每日推送、管理面板。
> **v1.0.0** — 正式版

---

## 技术栈

| 层 | 技术 |
|---|---|
| 前端 | Angular 21 (standalone components, signals, SCSS) |
| 后端 | ASP.NET Core 10.0 Web API (C#) |
| 数据库 | SQLite (EF Core, `EnsureCreated` + 启动时增量迁移) |
| 认证 | JWT (登录用户) + `X-User-Token` header (匿名用户) |
| 移动端 | Capacitor 8 (Android) |
| 邮件 | SMTP (支持 QQ邮箱 / 企业微信等) |

---

## 功能概览

### 核心功能
- 🫧 **投瓶** — 发文字+图片漂流瓶，支持匿名/署名、登录可见、评论仅瓶主可见
- 🌊 **捞瓶** — 随机获取瓶子，点赞、评论、楼中楼回复
- 📰 **每日推送** — 管理员发布新闻/故事/问答，首页展示
- 🏠 **首页** — 推送卡片 + 最新漂流 TOP10 + 热门浪花 TOP10（热度算法）

### 用户系统
- ✉️ **邮箱验证注册** — 注册后邮件激活，SMTP 可配置开关
- 🔑 **登录/找回密码** — 用户名或邮箱登录，邮件重置密码
- 👤 **个人设置** — 修改密码、更换邮箱、通知偏好、站点偏好
- 🔔 **通知中心** — 点赞/评论/系统通知，未读角标，浏览器通知权限

### 管理面板
- 📋 **瓶子管理** — 查看/删除/关闭/打开瓶子，查看评论和用户信息
- 👥 **用户管理** — 搜索/封禁/解封/改邮箱/改角色
- 📢 **通知推送** — 向全员或指定用户发送系统通知
- 📊 **管理日志报表** — 全站操作日志分页查看
- ⚙️ **站点设置** — 站点名称/版权/SMTP 配置

### 移动端
- 📱 **Android APK** — Capacitor 打包，支持返回键导航、图标和启动画面
- 🔙 **再按一次退出** — 底部 Toast 提示

---

## 快速启动

### 环境要求
- .NET 10.0 SDK
- Node.js 22+
- Angular CLI 21

### 开发环境

```bash
# 后端 (监听 http://localhost:5185)
dotnet run --project backend --launch-profile http

# 前端 (监听 http://localhost:4200，代理 API 到后端)
cd frontend && npm start
```

### 生产构建

```bash
# 一键打包到 deploy/
python publish.py

# 服务器运行
dotnet Aquarius.Api.dll   # 监听 0.0.0.0:5111
```

### Android 打包

```bash
cd frontend

# 本地局域网测试
build-android-local.bat

# 生产服务器（需先创建 src/environments/environment.server.ts）
build-android-server.bat
```

---

## 项目结构

```
Aquarius/
├── backend/                  # .NET 后端
│   ├── Controllers/          # API 控制器
│   ├── Models/               # 数据模型
│   ├── Data/                 # 数据库上下文
│   ├── Dtos/                 # 请求/响应 DTO
│   ├── Services/             # 邮件服务
│   └── Program.cs            # 启动配置 + 迁移
├── frontend/                 # Angular 前端
│   └── src/app/
│       ├── pages/            # 页面组件 (lazy loaded)
│       │   ├── home/         # 首页
│       │   ├── pick/         # 捞瓶
│       │   ├── throw/        # 投瓶
│       │   ├── daily/        # 每日推送
│       │   ├── my/           # 我的
│       │   ├── admin/        # 管理面板
│       │   ├── login/        # 登录
│       │   ├── register/     # 注册
│       │   ├── settings/     # 个人设置
│       │   ├── notifications/# 通知中心
│       │   ├── bottle-detail/# 瓶子详情
│       │   ├── verify-email/ # 邮箱验证
│       │   └── reset-password/# 重置密码
│       ├── components/       # 可复用组件
│       ├── services/         # API/认证/图片服务
│       ├── pipes/            # 自定义管道
│       └── guards/           # 路由守卫
├── deploy/                   # 生产部署包 (publish.py 生成)
├── upgrade-db.sql            # 数据库升级脚本
└── publish.py                # 一键打包脚本
```

---

## 配置

### SMTP 邮件
管理面板 → 站点设置 → SMTP 邮件配置，填写服务器/端口/用户名/授权码。

### 环境变量
| 文件 | 用途 |
|------|------|
| `frontend/src/environments/environment.ts` | 开发环境（apiBase 为空，走代理） |
| `frontend/src/environments/environment.prod.ts` | 生产环境（apiBase 为空，同域部署） |
| `frontend/src/environments/environment.local.ts` | Android 本地测试（指向局域网 IP） |
| `frontend/src/environments/environment.server.ts` | Android 生产（指向服务器域名，gitignore） |

### Android 图标
将 `aquarius_logo.png` 和 `aquarius_launcher.png` 放在 `frontend/` 目录下，构建脚本会自动复制到 Android 资源目录。

---

## 数据库迁移

项目使用 EF Core `EnsureCreated` + 启动时 `Migrate()` 方法进行增量迁移。新增字段只需：
1. 在 `Models/` 中添加属性
2. 在 `Program.cs` 的 `Migrate()` 方法中添加 `ALTER TABLE` SQL
3. 同步更新 `upgrade-db.sql`

---

## API 概览

| 端点 | 说明 |
|------|------|
| `POST /api/auth/register` | 注册（发送验证邮件） |
| `POST /api/auth/login` | 登录 |
| `POST /api/auth/verify-email` | 邮箱验证 |
| `POST /api/auth/forgot-password` | 忘记密码 |
| `POST /api/auth/reset-password` | 重置密码 |
| `GET /api/bottles/random` | 随机捞瓶 |
| `POST /api/bottles` | 投瓶 |
| `POST /api/bottles/{id}/like` | 点赞/取消 |
| `GET /api/bottles/{id}/logs` | 操作日志 |
| `GET /api/bottles/{bottleId}/comments` | 获取评论 |
| `POST /api/bottles/{bottleId}/comments` | 发表评论 |
| `GET /api/home` | 首页数据 |
| `GET /api/notifications` | 通知列表 |
| `GET /api/notifications/unread-count` | 未读计数 |
| `GET /api/users/preferences` | 个人偏好 |
| `GET /api/admin/*` | 管理面板（需管理员） |

---

## 许可

MIT License

---

## 更新日志

### v1.0.0 (2026-06-26)
- 正式版发布
- 邮件验证注册、SMTP 邮件系统
- 通知中心 + 未读角标 + 浏览器通知
- 首页（推送区 + 最新/热门 TOP10）
- 管理面板完整功能
- Android Capacitor 打包
- 智能返回导航、图片缩放、返回键适配
