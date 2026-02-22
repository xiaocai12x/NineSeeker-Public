# Celeste-Style Procedural Hair for Unity 2D

# 类蔚蓝风格程序化头发 · Unity 2D

Procedural hair system for Unity 2D: chain physics, per-frame mesh generation, state-driven color. Main hair + optional bangs (sprite) and tail (mesh). Single source of truth for color; no sprite animation required for the main hair.

Unity 2D 程序化头发：链式物理、每帧生成 Mesh、状态驱动发色。主发团 + 可选刘海（Sprite）与猫尾（Mesh）。颜色单一来源，主发团无需序列帧。

---

## 开源说明 / Open Source

### 中文

- 本仓库中的 Hair 相关代码（`HairController.cs`、`HairBangsController.cs`、`TailController.cs`）可自由用于个人或商业项目。
- 使用或参考时欢迎注明来源或致谢，但非强制。
- 代码按「现状」提供，不提供任何明示或暗示的保证；使用带来的风险由使用者自行承担。
- 若你基于本实现做了改进并开源，欢迎保留本说明或注明衍生关系。

### English

- The Hair-related code in this repository (`HairController.cs`, `HairBangsController.cs`, `TailController.cs`) may be used freely in personal or commercial projects.
- Attribution or credit is appreciated but not required.
- The code is provided "as is" without warranty of any kind; use at your own risk.
- If you fork or adapt this implementation and open-source it, you may keep this notice or credit the original source.

---

## 使用教程 / Usage Guide

### 1. 需求 / Requirements

| 需求 / Requirement | 说明 / Description |
|--------------------|---------------------|
| Unity | 推荐 2021.3+，需支持 URP 2D / 2D Renderer |
| 玩家脚本 | 你的玩家需提供：朝向、是否死亡、状态机（如 Crouch/Dash）、体力与可冲刺判断、Visuals 根节点 |
| 锚点 | 主发团/刘海/尾巴需要两个 Transform（左/右发根），通常由动画机 K 帧驱动 |

Your player script must expose: facing direction, IsDead, state machine (e.g. CrouchState, DashState), stamina / "can dash" check, and a Visuals root Transform. The hair/tail need anchor Transforms (left/right), typically driven by the Animator.

---

### 2. 快速开始 / Quick Start

**中文**

1. 将本仓库中的三个脚本复制到你的项目中（例如 `Assets/Scripts/Player/Hair/`）。
2. 若你的玩家类不叫 `PlayerController`，在 `HairController`、`HairBangsController`、`TailController` 中把 `PlayerController` 的引用改为你的玩家类，或抽成接口后注入。
3. 在场景中创建三个空物体，分别挂载主发团、刘海、尾巴的组件，并按下方「场景设置」连线。

**English**

1. Copy the three scripts from this repo into your project (e.g. `Assets/Scripts/Player/Hair/`).
2. If your player class has a different name, replace `PlayerController` references in the three scripts with your class, or introduce an interface and inject it.
3. Create three GameObjects in the scene, add the main hair / bangs / tail components, and wire them as in "Scene Setup" below.

---

### 3. 场景设置 / Scene Setup

**主发团 Main hair**

- 空物体 + **MeshFilter** + **MeshRenderer** + **HairController**。
- 在 HairController 上指定：
  - **Hair Anchor Right / Left**：两个发根锚点（通常挂在角色骨骼/Visuals 下，由动画 K 帧）。
  - **Hair Circle Sprite**（可选）：8×8 像素圆，用作 Mesh 纹理。
  - **Accessory Renderers**（可选）：需要跟发色同步的 SpriteRenderer（如耳朵）拖入。
- 在 Inspector 中为 MeshRenderer 指定 **URP 2D Sprite Unlit** 类 Shader（或手动指定 `hairShader`），避免 Build 时被剥离。

**刘海 Bangs**

- 带 **SpriteRenderer** 的物体 + **HairBangsController**。
- 指定 **Hair Master** = 主发团的 HairController。
- 指定 **Hair Anchor Right / Left**（可与主发团共用）。
- 配置 **Sprite Mapping**：`animationKeyword`（玩家当前动画精灵名包含的关键字）→ `bangsSprite`；未匹配时使用 **Default Bangs**。
- 若使用自定义 Shader（如 `Custom/Bangs_Luminance_Sync`），需在 Build 中保留该 Shader。

**尾巴 Tail**

- 空物体 + **MeshFilter** + **MeshRenderer** + **TailController**。
- 指定 **Tail Anchor Right / Left**、**Hair Master**、贴图（如 8×8 圆）、段数/半径/物理参数等。
- 可选 **Use Darken Effect** + **Darken Multiplier** 使尾巴略深于发色。

---

### 4. 接入游戏逻辑 / Integration

脚本中供外部调用的接口集中在 **HairController** 的 `#region 供外部调用` 中：

| 接口 API | 说明 Description |
|----------|------------------|
| `WarpNodes(Vector3 position)` | 将所有头发节点瞬移到指定世界坐标并刷新 Mesh。用于**重生/传送**时避免拉丝。 |
| `OnRefill()` | 触发补给闪光与脉冲。在**体力回复/补给**时调用。 |
| `CurrentHairColor` | 只读，当前插值后的发色。供尾巴、刘海、挂件、**死亡特效**等读取。 |
| `GetPulseScale()` | 只读，当前脉冲缩放。供 UI 等同步表现。 |

**调用示例 / Example**

- 重生时：`hair.WarpNodes(player.Visuals.transform.position);`
- 回体力时：`hair.OnRefill();`
- 死亡特效取色：`var color = player.GetComponentInChildren<HairController>()?.CurrentHairColor;`

---

### 5. Build 注意 / Build Notes

- **Hair**：在 Inspector 中为 HairController 指定 **hairShader**（URP 2D Sprite Unlit），避免依赖 `Shader.Find` 在 Build 中被裁掉。
- **Tail**：当前脚本内使用 `Shader.Find`，若 Build 后尾巴不显示（颜色丢失），可改为 SerializeField 暴露 Shader 并在 Inspector 中指定。
- **Linear 空间**：若 Project 使用 Linear Color Space，脚本已对发色做 `.linear` 再写入 Mesh/挂件，一般无需额外处理。
- **刘海 Shader**：若使用自定义 Shader，请在 Project Settings → Graphics 或 Shader  stripping 中确保该 Shader 被包含。

---

### 6. 文件清单 / File List

| 文件 File | 职责 Role |
|-----------|-----------|
| `HairController.cs` | 主发团：链式物理、Mesh 生成、颜色主控、挂件同步；对外提供 WarpNodes、OnRefill、CurrentHairColor、GetPulseScale。 |
| `HairBangsController.cs` | 刘海：锚点跟随、按动画关键字切换贴图、压扁、从 Hair 同步颜色。 |
| `TailController.cs` | 猫尾：独立链式物理与 Mesh，从 Hair 同步颜色（可加深）。 |

---

### 7. 打包需包含的 Shader / Shaders to Include When Packaging

**中文**

- **需要随仓库提供的**：**`Bangs_Luminance_Sync.shader`**（建议路径：`Assets/Shader/Bangs_Luminance_Sync.shader`）。刘海通过 `Shader.Find("Custom/Bangs_Luminance_Sync")` 使用该 Shader；若不包含此文件，他人克隆后刘海会找不到 Shader，显示异常。
- **不需要打包的**：主发团与尾巴使用的 `Universal Render Pipeline/2D/Sprite-Unlit-Default` 为 URP 内置 Shader，用户项目安装 URP 即可；仅在文档中说明在 Inspector 指定该 Shader（或保证 Build 不剥离）即可。

**English**

- **Include in repo**: **`Bangs_Luminance_Sync.shader`** (e.g. `Assets/Shader/Bangs_Luminance_Sync.shader`). Bangs use `Shader.Find("Custom/Bangs_Luminance_Sync")`; without this file, the shader will be missing and bangs will not display correctly.
- **No need to include**: The main hair and tail use URP's built-in `Universal Render Pipeline/2D/Sprite-Unlit-Default`; users only need a project with URP and to assign that shader in Inspector (or ensure it is not stripped in Build).

---

*如有问题或改进建议，欢迎提 Issue 或 PR。*
