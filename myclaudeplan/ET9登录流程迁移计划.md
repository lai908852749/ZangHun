# ET9登录流程迁移计划文档

## 项目背景
- **源项目路径**: F:\A_YIUI_Tutorial_ET9\T_YIUI_ET9
- **目标项目路径**: F:\AA_MyET10Project
- **目标**: 将源项目的完整登录流程迁移到当前项目

## 现状分析

### 当前项目登录流程（简化版）
```
1. 用户输入账号密码
2. 直接登录进入游戏
3. 设置PlayerComponent.MyId
4. 发布LoginFinish事件
```

### 源项目登录流程（完整版）
```
1. 用户输入账号密码
2. 登录获取Token（C2R_Login）
3. 获取服务器列表（C2R_GetServerInfos）
4. 获取角色列表（C2R_GetRoles）
5. 如无角色则创建（C2R_CreateRole）
6. 获取RealmKey（C2R_GetRealmKey）
7. 使用RealmKey登录游戏（LoginGameAsync）
8. 设置PlayerComponent.MyId
9. 发布LoginFinish事件
```

## 核心差异

### 1. 账号系统架构
- **源项目**: 完整的cn.etetet.account包，包含服务器信息、角色管理、Token验证等
- **当前项目**: 基础的cn.etetet.login包，仅有简单登录功能

### 2. 服务器架构
- **源项目**: 三层架构（LoginCenter、Realm、Gate）
- **当前项目**: 简化的单层架构

### 3. 功能对比
| 功能 | 源项目 | 当前项目 |
|------|--------|----------|
| Token验证 | ✓ | ✗ |
| 服务器列表 | ✓ | ✗ |
| 角色管理 | ✓ | ✗ |
| RealmKey | ✓ | ✗ |
| 多服务器支持 | ✓ | ✗ |

## 迁移方案

### 阶段一：核心功能迁移（必需）

#### 1.1 迁移cn.etetet.account包
- 复制整个包目录：`F:\A_YIUI_Tutorial_ET9\T_YIUI_ET9\Packages\cn.etetet.account`
- 目标位置：`F:\AA_MyET10Project\Packages\cn.etetet.account`
- 包含内容：
  - Scripts/Model（Entity定义）
  - Scripts/Hotfix（System实现）
  - Proto（协议文件）

#### 1.2 更新协议文件
需要迁移的Proto文件：
- `LoginInner_S_21000.proto` - 内部服务器通信
- `LoginOuter_C_1000.proto` - 客户端服务器通信

#### 1.3 更新客户端登录逻辑
替换文件：
- `ClientSenderComponentSystem.cs` - 添加完整的登录方法
- `LoginHelper.cs` - 实现完整登录流程

### 阶段二：服务器端支持（必需）

#### 2.1 LoginCenter场景
添加组件和Handler：
```
LoginInfoRecordComponent - 登录记录管理
G2L_AddLoginRecordHandler - 添加登录记录
G2L_RemoveLoginRecordHandler - 移除登录记录
R2L_LoginAccountRequestHandler - 账号登录请求
```

#### 2.2 Realm服务
添加功能：
```
TokenComponent - Token管理
AccountSessionsComponent - 账号会话管理
C2R_LoginAccountHandler - 账号登录
C2R_CreateRoleHandler - 创建角色
C2R_DeleteRoleHandler - 删除角色
C2R_GetRolesHandler - 获取角色列表
C2R_GetServerInfosHandler - 获取服务器列表
C2R_GetRealmKeyHandler - 获取RealmKey
```

#### 2.3 Gate服务
更新功能：
```
GateSessionKeyComponent - 会话密钥管理
C2G_LoginGateHandler - Gate登录验证
C2G_LoginGameGateHandler - 游戏登录
R2G_GetLoginKeyHandler - 获取登录密钥
```

### 阶段三：配置调整

#### 3.1 包依赖配置
更新package.json，处理包依赖关系：
- cn.etetet.account包需要依赖：
  - cn.etetet.core
  - cn.etetet.proto
  - cn.etetet.startconfig
  - cn.etetet.http

#### 3.2 服务器配置
更新StartConfig，添加：
- LoginCenter进程配置
- Realm进程配置
- Gate进程更新

### 阶段四：UI优化（可选）

#### 4.1 扩展登录界面
- 添加服务器选择下拉框
- 显示服务器状态

#### 4.2 角色管理界面
- 角色列表显示
- 角色创建面板
- 角色删除确认

## 具体文件清单

### 需要完整复制的目录
```
F:\A_YIUI_Tutorial_ET9\T_YIUI_ET9\Packages\cn.etetet.account\
```

### 需要更新的现有文件
```
F:\AA_MyET10Project\Packages\cn.etetet.login\Scripts\Hotfix\Client\Login\LoginHelper.cs
F:\AA_MyET10Project\Packages\cn.etetet.login\Scripts\Hotfix\Client\Login\ClientSenderComponentSystem.cs
F:\AA_MyET10Project\Packages\cn.etetet.wow\Scripts\HotfixView\Client\YIUISystem\Login\LoginPanelComponentSystem.cs
```

### 需要添加的Proto文件
```
LoginInner_S_21000.proto
LoginOuter_C_1000.proto
```

## 风险评估及解决方案

### 风险1：包ID冲突
- **问题**: 两个项目的包ID都是9
- **解决**: 修改cn.etetet.account的packagegit.json，分配新的ID

### 风险2：依赖关系复杂
- **问题**: cn.etetet.account有多层依赖
- **解决**: 逐层检查并更新package.json依赖配置

### 风险3：协议编号冲突
- **问题**: Proto文件编号可能冲突
- **解决**: 检查并调整协议编号，确保唯一性

### 风险4：数据持久化
- **问题**: 角色数据需要持久化存储
- **解决**: 配置MongoDB或其他数据库支持

## 测试验证清单

### 功能测试
- [ ] 新用户注册并登录
- [ ] 获取服务器列表
- [ ] 创建首个角色
- [ ] 角色列表显示
- [ ] 选择角色进入游戏
- [ ] 重复登录检测
- [ ] Token过期处理

### 异常测试
- [ ] 网络断开重连
- [ ] 服务器不可用
- [ ] 账号密码错误
- [ ] 角色创建失败
- [ ] 并发登录处理

### 性能测试
- [ ] 登录响应时间
- [ ] 批量用户登录
- [ ] 服务器负载测试

## 执行步骤

### Step 1: 备份当前项目
```bash
cp -r F:\AA_MyET10Project F:\AA_MyET10Project_backup
```

### Step 2: 复制account包
```bash
cp -r F:\A_YIUI_Tutorial_ET9\T_YIUI_ET9\Packages\cn.etetet.account F:\AA_MyET10Project\Packages\
```

### Step 3: 更新包ID
编辑 `F:\AA_MyET10Project\Packages\cn.etetet.account\packagegit.json`，修改ID为未使用的值

### Step 4: 更新依赖
编辑各包的package.json，添加必要的依赖关系

### Step 5: 编译验证
```bash
dotnet build ET.sln
```

### Step 6: 启动服务器测试
按照更新后的配置启动所有必要的服务器进程

### Step 7: 客户端测试
在Unity中运行客户端，测试完整登录流程

## 回滚方案

如果迁移失败，执行以下步骤回滚：
1. 删除新添加的cn.etetet.account包
2. 恢复原始的LoginHelper.cs和ClientSenderComponentSystem.cs
3. 恢复原始的服务器配置
4. 重新编译项目

## 时间估算

- 阶段一（核心功能）：2-3小时
- 阶段二（服务器端）：3-4小时
- 阶段三（配置调整）：1-2小时
- 阶段四（UI优化）：2-3小时（可选）
- 测试验证：2-3小时

**总计**: 8-12小时（不含UI优化）

## 后续优化建议

1. **安全增强**
   - 添加登录尝试次数限制
   - 实现账号冻结机制
   - 加强Token安全性

2. **用户体验**
   - 添加记住密码功能
   - 实现自动重连机制
   - 优化登录加载提示

3. **运维支持**
   - 添加登录日志记录
   - 实现在线用户统计
   - 支持服务器维护公告

---

**文档版本**: 1.0
**创建日期**: 2025-09-16
**作者**: Claude AI Assistant