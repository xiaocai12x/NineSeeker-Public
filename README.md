# NineSeek · Unity 2D 可复用系统

![Unity Version](https://img.shields.io/badge/Unity-6%2B-blue) ![License](https://img.shields.io/badge/License-MIT-green)

本仓库包含三套独立可复用的系统：**死亡重生管线 (DeathFlow)**、**程序化头发系统 (Procedural Hair)** 与 **移动平台模块 (Moving Platform)**。点击下方标题展开对应教程。

---

<details>
<summary><strong>💀 DeathFlow：像《蔚蓝》一样丝滑的死亡重生管线</strong></summary>

在硬核平台跳跃游戏中，死亡不应是挫败，而应是极低成本的试错。
通过 **策略模式 (Strategy Pattern)** 与 **事件解耦 (Pub-Sub)**，将角色的"死与生"封装为一段高度可定制的生命周期。让你在不写一行 `if-else` 屎山的前提下，轻松实现极其丝滑、0.5 秒极速复位、且死法各异的游戏体验。

### ✨ 核心特性

* **⏱️ 极速复位**：从死亡到重生仅需约 0.5 秒，肌肉记忆不中断。
* **🎨 表现解耦**：基于 `ScriptableObject` 的策略资产，掉坑、被刺、挤压… 不同死法独立配置，策划可在 Inspector 中即插即用。
* **🔌 事件驱动**：Player 仅负责广播 `OnPlayerDied` 事件，复活流程交由 `DeathRespawnManager` 统一编排，彻底解耦。
* **⚡ 零实例化**：复活过程不切场景、不 `Instantiate` 新角色，纯粹基于状态与物理重置，性能开销极低。

### 🏗️ 架构概览：四角色生命周期

1. **凶手 (Hazard/Level)**：关卡内的致死机关，触发死亡并传递「死亡剧本 (DeathStrategy)」。
2. **受害者 (PlayerController)**：受到致命伤后广播静态事件，关闭自身物理交互，等待复活。
3. **导演 (DeathRespawnManager)**：订阅死亡事件，编排四阶段流程（死 → 黑屏转场 → 移动 → 亮屏复活）。
4. **剧本 (DeathStrategy)**：策略资产，定义各阶段的具体特效、音效与动画。

### 🚀 核心流程 (The 4-Stage Death Flow)

| 阶段 | 方法名 | 职责描述 |
| :--- | :--- | :--- |
| **1. 死亡演出** | `ExecuteDeath()` | 物理定格、受击特效/音效、顿帧 (HitStop)。 |
| **2. 屏幕转场** | `TransitionIn()` | 屏幕拉黑（如 Iris 材质收缩），遮蔽视野。 |
| **3. 复活准备** | `ExecuteRespawn()` | 位置与速度已由 Manager 在本阶段前重置至 Checkpoint；策略只做复活准备（如缩放/表现）并**保持隐身**。 |
| **4. 真正复活** | `TransitionOut()` | 屏幕亮起，聚拢特效，调用 `ReviveInternal()` 恢复控制权并重置状态机、体力、碰撞等。 |

### 🛠️ 如何扩展一种新的死亡表现？

1. 新建 C# 脚本，继承 `DeathStrategy`。
2. 重写四个阶段的协程逻辑。
3. 添加 `[CreateAssetMenu]`，在编辑器中右键创建策略资产。
4. 将资产拖到对应机关（如毒水池）的 Inspector。完成！
   
</details>

---

<details>
<summary><strong>🍓 Celeste-Style 程序化头发系统 (Procedural Hair)</strong></summary>

类蔚蓝风格程序化头发系统 · Unity 2D。链式物理、每帧 Mesh 生成、状态驱动发色同步。**主发团无需任何序列帧动画。**

### ✨ 核心特性

* **⛓️ 链式物理计算**：主发团与尾巴基于物理节点跟随，运动丝滑。
* **🎨 颜色单一数据源**：发色由当前状态（冲刺、无体力等）驱动，刘海、尾巴、耳朵等挂件自动同步主发色，无需 K 帧变色动画。
* **🧩 高度模块化**：主发团 (Main Hair)、刘海 (Bangs)、尾巴 (Tail) 可独立拆装。
* **⚡ 易扩展**：内置 `WarpNodes` 解决传送拉丝，`OnRefill` 兼容体力恢复/吃草莓特效。

### 📜 开源说明

**[CN]** 本仓库中的 Hair 相关代码（`HairController`、`HairBangsController`、`TailController`）可自由用于个人或商业项目。使用或参考时欢迎注明来源，非强制。代码按「现状」提供，风险自负。若衍生开源，欢迎保留本说明。  
**[EN]** Hair-related code may be used freely in personal or commercial projects. Attribution appreciated but not required. Provided "as is" without warranty.

### 🛠️ 核心模块与文件

| 文件 | 职责 |
| --- | --- |
| **`HairController.cs`** | 主发团：链式物理、Mesh 生成、颜色主控、挂件同步。对外提供 `WarpNodes`、`OnRefill`、`CurrentHairColor`、`GetPulseScale`。 |
| **`HairBangsController.cs`** | 刘海：锚点跟随、按动画关键字切换贴图、从主发团同步颜色与压扁。 |
| **`TailController.cs`** | 猫尾：独立链式物理与 Mesh，从主发团同步颜色（可配置 `Use Darken Effect` / `Darken Multiplier`）。 |

### 🚀 场景配置指南

**前置**：Unity 2021.3+，URP 2D。玩家需提供朝向、IsDead、状态机、体力、Visuals 根节点；动画骨骼下需有左/右发根锚点。

**主发团**

* 空物体 + `MeshFilter` + `MeshRenderer` + `HairController`。
* 设置 **Hair Anchor Right / Left**，**Hair Circle Sprite**（8×8 像素圆），**Accessory Renderers**（需同步发色的 SpriteRenderer，如兽耳）。
* MeshRenderer 指定 **URP 2D Sprite Unlit**（或 `Universal Render Pipeline/2D/Sprite-Unlit-Default`），避免 Build 剥离。

**刘海**

* 物体 + `SpriteRenderer` + `HairBangsController`。**Hair Master** 指向主发团。
* **Sprite Mapping**：配置 `animationKeyword` → `bangsSprite`，未匹配时用 **Default Bangs**。
* 使用自定义 Shader `Custom/Bangs_Luminance_Sync`（见 `Assets/Shader/Bangs_Luminance_Sync.shader`），打包时请在 **Project Settings → Graphics → Always Included Shaders** 中加入该 Shader。

**尾巴（可选）**

* 空物体 + `MeshFilter` + `MeshRenderer` + `TailController`。指定 **Tail Anchor Right / Left**、**Hair Master**。
* 勾选 **Use Darken Effect**、调节 **Darken Multiplier** 可让尾巴略深于发色。

### 🔌 核心 API (HairController)

| API | 用途 |
| --- | --- |
| `WarpNodes(Vector3 position)` | 重生/传送时调用，瞬移所有头发节点，防止拉丝。 |
| `OnRefill()` | 体力恢复/吃补给时调用，触发白光闪烁与缩放脉冲。 |
| `CurrentHairColor` | 只读，当前插值发色。供死亡特效、UI 等读取。 |
| `GetPulseScale()` | 只读，当前脉冲缩放。供外部动画同步。 |

**示例**

```csharp
hairController.WarpNodes(player.Visuals.transform.position);  // 重生
hairController.OnRefill();                                    // 回体力
main.startColor = hairController.CurrentHairColor;            // 死亡粒子取色
```

### ⚠️ 打包注意

* **主发团 / 尾巴**：在 Inspector 中为 MeshRenderer 手动指定 URP Sprite Unlit Shader（或 HairController 的 `hairShader` 字段），避免依赖 `Shader.Find` 在 Build 中被裁掉。
* **刘海**：将 `Bangs_Luminance_Sync.shader` 加入 **Always Included Shaders**，防止打包丢失。
* 脚本已对 Linear 颜色空间做 `.linear` 处理，一般无需额外 Gamma 转换。

</details>

---

<details>
<summary><strong>🚌 移动平台模块 (Moving Platform)：车票式接口 + 防抖防穿透</strong></summary>

2D 平台跳跃中，移动平台、电梯、齿轮、ZipMover 既要「带着玩家走」，又要支持起跳带走平台速度、被挤在墙边时压杀判定，且高速时不能穿透、抖动。本模块通过 **接口即车票** 的架构，实现平台与乘客职责分明、边界清晰，并彻底解决 Kinematic 高速移动时的抖动与穿透。

### ✨ 核心特性

* **🎫 车票式接口**：`IMovingPlatformRider` = 谁实现谁就能被平台搬运、被问压杀、被通知死亡。平台只认「有没有车票」，不关心是玩家、箱子还是 NPC，扩展新乘客无需改平台代码。
* **🔄 两条接口线**：**IMovingPlatformRider**（平台→乘客：ManualMove、CheckWallForCrush、DieByCrush）；**IVelocityProvider**（乘客问→平台答：GetVelocityAtPoint，起跳时叠加平台速度）。
* **⏱️ 先移乘客再移平台**：同一物理步内先对乘客 `ManualMove(delta)` 再更新平台位置，配合 FixedUpdate / 协程 `WaitForFixedUpdate()`，0 抖动、0 穿透。
* **🥪 压杀先判后推**：侧向推挤前先调 `CheckWallForCrush`，只有「推不动、前面是墙」才 `DieByCrush`，与 DeathFlow 策略资产无缝衔接。

### 🏗️ 架构概览：平台 · 中介 · 乘客

| 角色 | 职责 |
| :--- | :--- |
| **平台** | 自己算位移与速度，通过接口把结果交给对方；MovingPlatform、ZipMover、RotatingGear 等。 |
| **中介** | 接口契约 `IMovingPlatformRider` / `IVelocityProvider`，平台与乘客只依赖接口，不依赖具体类型。 |
| **乘客** | 实现接口，拿到平台给的数据后自己维护位置、接地、压杀预判、死亡流程；当前即 PlayerController。 |

### 🚀 平台通过车票给了什么、乘客怎么处理

| 平台通过接口给的 | 乘客拿到的信息 | 乘客怎么处理 |
| :--- | :--- | :--- |
| `ManualMove(delta)` | 本帧位移 **delta** | `RB.position += delta`；记平台帧、重力补偿、强制接地并切状态。 |
| `CheckWallForCrush(dir, dist)` | 推挤方向、距离（期待返回值） | 用自己碰撞盒 BoxCast，返回「会不会顶到墙」；平台据此决定 ManualMove 或 DieByCrush。 |
| `DieByCrush(strategy)` | 死亡策略 **strategy** | 调 `Die(zero, strategy)`，走统一死亡流程。 |
| *乘客问* `GetVelocityAtPoint(世界点)` | *平台答* 该点速度 | 起跳时脚下射线取接口，将返回值叠到起跳初速 X/Y，动量继承。 |

### 📐 平台侧铁律

* 位移、检测、搬运 **全在 FixedUpdate**（或协程 `WaitForFixedUpdate()`），与物理步一致。
* **先移乘客，再移平台**：同一物理步内先 `MovePassengers(delta)` 再 `_rb.position += moveAmount` / `MovePosition(nextPos)`。
* 平台 Rigidbody：**Kinematic** + **Continuous** 碰撞检测，减少高速穿透。

### 🛠️ 核心脚本与文档

| 类型 | 脚本/接口 | 作用 |
| :--- | :--- | :--- |
| 接口 | `IMovingPlatformRider` | 乘客：ManualMove、CheckWallForCrush、DieByCrush |
| 接口 | `IVelocityProvider` | 提供某点速度，用于起跳动量继承 |
| 平台 | `MovingPlatform` | 线性/圆周运动，MovePassengers + PushActors，压杀前 CheckWallForCrush |
| 高速 | `ZipMover` | 协程 + WaitForFixedUpdate，曲线驱动，齿轮/链条/交通灯等表现层；|
| 旋转 | `RotatingGear` | GetVelocityAtPoint 切线速度，FixedUpdate 里 ManualMove(vel × fixedDeltaTime) |


</details>

---

*如有问题或改进建议，欢迎提交 Issue 或 PR。*
