# ET框架 Proto消息规范文档

## 一、文件命名规则

### 1.1 命名格式
```
{模块名}_{类型标识}_{起始编号}.proto
```

### 1.2 类型标识说明
- `C` - Client消息（客户端相关）
- `S` - Server消息（服务端内部）
- `CS` - 客户端服务端共用消息

### 1.3 编号规则
- 使用100的倍数作为起始编号
- 每个包独占一个编号段，避免冲突
- 示例：
  - `Login_C_10000.proto` - 登录模块客户端消息，从10000开始
  - `Router_C_2000.proto` - 路由模块客户端消息，从2000开始
  - `ActorLocation_S_20600.proto` - ActorLocation服务端消息，从20600开始
  - `MapManager_S_20800.proto` - MapManager服务端消息，从20800开始

## 二、Proto文件结构

### 2.1 基本结构
```protobuf
syntax = "proto3";
package ET;

// 消息定义...
```

### 2.2 Proto文件位置规则
- 如果包中已经存在Proto文件夹且有消息文件，应在已有文件中新增消息，不创建新文件
- 只有当包中没有Proto文件夹或需要新的消息类型时才创建新文件
- 保持消息的聚合性，相关消息应放在同一文件中

### 2.3 请求响应对模板
```protobuf
// ResponseType M2C_XXXResponse
message C2M_XXXRequest // ILocationRequest
{
    int32 RpcId = 1;         // 必须，字段1
    // 业务字段从2开始
    int32 Param1 = 2;
    string Param2 = 3;
}

message M2C_XXXResponse // ILocationResponse
{
    int32 RpcId = 1;         // 必须，字段1
    int32 Error = 2;         // 必须，字段2
    string Message = 3;      // 必须，字段3
    // 业务字段从4开始
    int32 Result = 4;
}
```

### 2.4 单向消息模板
```protobuf
message M2C_Notification // IMessage
{
    int64 UnitId = 1;
    int32 Type = 2;
    string Content = 3;
}
```

## 三、消息命名规范

### 3.1 客户端与服务器通信前缀

| 前缀 | 说明 | 示例 |
|------|------|------|
| `C2M_` | 客户端→Map服务器 | `C2M_AcceptQuest` |
| `M2C_` | Map服务器→客户端 | `M2C_CreateUnits` |
| `C2G_` | 客户端→Gate服务器 | `C2G_LoginGate` |
| `G2C_` | Gate服务器→客户端 | `G2C_EnterMap` |
| `C2R_` | 客户端→Realm服务器 | `C2R_Login` |
| `R2C_` | Realm服务器→客户端 | `R2C_Login` |

### 3.2 服务器间通信前缀

| 前缀 | 说明 | 示例 |
|------|------|------|
| `A2M_` | 任意服务器→Map服务器 | `A2M_Reload` |
| `M2A_` | Map服务器→任意服务器 | `M2A_Reload` |
| `A2MapManager_` | 任意服务器→MapManager | `A2MapManager_GetMapRequest` |
| `MapManager2Map_` | MapManager→Map服务器 | `MapManager2Map_NotifyPlayerTransferRequest` |
| `Map2MapManager_` | Map服务器→MapManager | `Map2MapManager_LogoutRequest` |
| `M2M_` | Map服务器→Map服务器 | `M2M_UnitTransferRequest` |
| `G2Map_` | Gate服务器→Map服务器 | `G2Map_Logout` |
| `Map2G_` | Map服务器→Gate服务器 | `Map2G_Logout` |

### 3.3 特殊服务通信前缀

| 前缀 | 说明 | 示例 |
|------|------|------|
| `Main2NetClient_` | 主线程→网络客户端 | `Main2NetClient_Login` |
| `NetClient2Main_` | 网络客户端→主线程 | `NetClient2Main_Login` |
| `Console2Robot_` | 控制台→机器人 | `Console2Robot_LogoutRequest` |
| `RobotCase_` | 机器人测试用例 | `RobotCase_001_PrepareData_Request` |

### 3.4 命名规则总结
- 使用PascalCase（大驼峰）命名
- 名称要清晰表达消息用途
- Request/Response成对出现
- 服务器间通信使用具体服务名或通用标识（A表示Any）
- 避免过长的名称

## 四、接口类型规范

### 4.1 接口类型说明

通过注释标注消息实现的接口：

| 接口类型 | 用途 | 特点 |
|----------|------|------|
| `IMessage` | 普通单向消息 | 无需响应 |
| `IRequest/IResponse` | 基础RPC对 | 需要响应 |
| `ILocationRequest/ILocationResponse` | Location服务RPC | 带位置信息 |
| `ISessionRequest/ISessionResponse` | Session RPC | 会话级别 |
| `IActorRequest/IActorResponse` | Actor RPC | Actor模式 |
| `IRobotCaseMessage` | 机器人测试消息 | 测试专用 |

### 4.2 使用示例
```protobuf
// 单向消息
message M2C_BuffAdd // IMessage
{
    int64 UnitId = 1;
    int64 BuffId = 2;
}

// Location请求响应
message C2M_AcceptQuest // ILocationRequest
{
    int32 RpcId = 1;
    int32 QuestId = 2;
}

message M2C_AcceptQuest // ILocationResponse
{
    int32 RpcId = 1;
    int32 Error = 2;
    string Message = 3;
}

// 服务器间通信
message A2MapManager_GetMapRequest // IRequest
{
    int32 RpcId = 1;
    string MapName = 2;
    int64 MapId = 3;
    int64 UnitId = 4;
}
```

## 五、字段定义规范

### 5.1 字段命名
- **必须**使用PascalCase（大驼峰）
- 字段名要清晰表达含义
- 避免缩写，除非是通用缩写（如Id）

### 5.2 字段编号规则
- 从1开始递增
- 不要跳号，不要重复
- 标准字段固定编号：
  - `RpcId = 1` （请求/响应必须）
  - `Error = 2` （响应必须）
  - `Message = 3` （响应必须）
  - 业务字段从4开始

### 5.3 字段类型

#### 基础类型
```protobuf
int32 Count = 1;
int64 PlayerId = 2;
string Name = 3;
bool IsActive = 4;
float Progress = 5;
bytes Data = 6;
```

#### 复杂类型
```protobuf
// 列表/数组
repeated int32 ItemIds = 1;
repeated QuestInfo QuestList = 2;

// 字典/映射
map<int32, int64> KV = 1;
map<string, PlayerInfo> Players = 2;

// 嵌套消息
UnitInfo Unit = 1;

// ET框架特殊类型
ActorId ActorId = 1;  // Actor标识
```

#### Unity专用类型
```protobuf
Unity.Mathematics.float3 Position = 1;
Unity.Mathematics.quaternion Rotation = 2;
```

### 5.4 字段注释
```protobuf
message QuestInfo
{
    int64 QuestId = 1;              // 任务唯一ID
    int32 Status = 2;                // 任务状态：0=未开始，1=进行中，2=已完成
    repeated QuestObjectiveInfo Objectives = 3;  // 任务目标列表
    int64 AcceptTime = 4;            // 接取时间（时间戳）
    int64 CompleteTime = 5;          // 完成时间（时间戳）
}
```

## 六、复用消息定义

### 6.1 通用消息结构
定义可复用的消息结构减少重复：

```protobuf
// 通用奖励结构
message RewardInfo
{
    int32 Type = 1;     // 奖励类型：1=经验，2=金币，3=道具
    int32 ItemId = 2;   // 道具ID（类型为道具时使用）
    int32 Count = 3;    // 数量
}

// 在多个消息中复用
message M2C_QuestComplete // IMessage
{
    int32 QuestId = 1;
    repeated RewardInfo Rewards = 2;  // 复用RewardInfo
}

message M2C_ClaimAchievement // ILocationResponse
{
    int32 RpcId = 1;
    int32 Error = 2;
    string Message = 3;
    repeated RewardInfo Rewards = 4;  // 复用RewardInfo
}
```

## 七、代码生成规范

### 7.1 自动生成特性
Proto2CS工具会自动为消息生成以下特性：
- `[MemoryPackable]` - 序列化支持
- `[Message(Opcode.XXX)]` - 消息操作码
- `[ResponseType(nameof(XXX))]` - 响应类型关联

### 7.2 对象池支持
自动生成的代码包含：
- `Create()` - 从对象池获取实例
- `Dispose()` - 回收到对象池

### 7.3 特殊注释
- `// no dispose` - 阻止自动生成Dispose方法
- `///` - 三斜杠注释转换为C# XML文档注释

## 八、最佳实践

### 8.1 消息分组
- 将功能相关的消息放在同一个proto文件
- 每个模块独立管理自己的proto文件
- 服务器间通信消息放在S类型文件中
- 避免单个文件过大（建议不超过500行）

### 8.2 版本兼容
- 不要删除或修改已有字段的编号
- 新增字段使用新的编号
- 废弃字段可以注释标记但保留编号

### 8.3 性能优化
- 合理使用repeated和map
- 避免深层嵌套结构
- 大数据量考虑分批传输
- 服务器间传输可使用bytes序列化整个对象

### 8.4 安全性
- 敏感数据考虑加密
- 验证数据合法性
- 避免传输不必要的信息

## 九、常见错误避免

1. ❌ 使用camelCase命名字段（应该用PascalCase）
2. ❌ RpcId、Error、Message字段编号错误
3. ❌ 忘记标注ResponseType
4. ❌ 请求响应消息命名不匹配
5. ❌ 字段编号重复或跳号
6. ❌ 接口类型注释错误
7. ❌ proto文件命名不符合规范
8. ❌ 起始编号不是100的倍数
9. ❌ 服务器间通信前缀使用错误

## 十、示例参考

### 10.1 客户端服务器通信示例
```protobuf
syntax = "proto3";
package ET;

// 接受任务请求
// ResponseType M2C_AcceptQuest
message C2M_AcceptQuest // ILocationRequest
{
    int32 RpcId = 1;
    int32 QuestId = 2;      // 任务配置ID
    int64 NPCId = 3;        // NPC实例ID
}

message M2C_AcceptQuest // ILocationResponse
{
    int32 RpcId = 1;
    int32 Error = 2;
    string Message = 3;
}
```

### 10.2 服务器间通信示例
```protobuf
syntax = "proto3";
package ET;

// 获取地图信息
// ResponseType A2MapManager_GetMapResponse
message A2MapManager_GetMapRequest // IRequest
{
    int32 RpcId = 1;
    string MapName = 2;
    int64 MapId = 3;
    int64 UnitId = 4;
}

message A2MapManager_GetMapResponse // IResponse
{
    int32 RpcId = 1;
    int32 Error = 2;
    string Message = 3;
    string MapName = 4;
    int64 MapId = 5;
    ActorId MapActorId = 6;
}

// Map服务器间单位传送
// ResponseType M2M_UnitTransferResponse
message M2M_UnitTransferRequest // IRequest
{
    int32 RpcId = 1;
    ActorId OldActorId = 2;
    bytes Unit = 3;              // 序列化的单位数据
    repeated bytes Entitys = 4;   // 相关实体数据
    bool ChangeScene = 5;
}

message M2M_UnitTransferResponse // IResponse
{
    int32 RpcId = 1;
    int32 Error = 2;
    string Message = 3;
    ActorId NewActorId = 4;
}
```

---

*本文档为ET框架Proto消息定义的官方规范，所有开发人员必须严格遵守。*

*最后更新：2025年9月*