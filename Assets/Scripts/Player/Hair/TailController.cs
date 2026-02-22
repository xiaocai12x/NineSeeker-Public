using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 猫尾链式网格：双锚点、物理跟随、下蹲压扁、与主发团颜色同步并可选加深。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TailController : MonoBehaviour
{
    [Header("Dual Anchors (尾巴锚点)")]
    /// <summary>朝右时尾巴根部锚点。</summary>
    public Transform tailAnchorRight;
    /// <summary>朝左时尾巴根部锚点。</summary>
    public Transform tailAnchorLeft;

    [Header("Cat Tail Appearance (猫尾外观)")]
    [Tooltip("拖入那张 8x8 的白色像素圆")]
    public Sprite circleSprite;
    /// <summary>尾巴段数，影响长度与圆滑度。</summary>
    public int segmentCount = 8;
    /// <summary>首段半径，后续按 tapering 递减。</summary>
    public float baseRadius = 0.25f;
    /// <summary>相邻节点最大间距，超过会被约束。</summary>
    public float nodeDistance = 0.15f;
    [Range(0, 1)]
    /// <summary>每段半径递减系数，越接近 1 越粗。</summary>
    public float tapering = 0.95f;

    [Header("Physics (物理调教)")]
    /// <summary>尾巴下垂重力。</summary>
    public float gravity = 5f;
    /// <summary>水平阻力。</summary>
    public float drag = 4f;
    /// <summary>节点跟随上一节点的 Lerp 速度。</summary>
    public float followSpeed = 15f;

    [Header("Crouch Squash")]
    [Range(0f, 1f)]
    /// <summary>下蹲时 Y 方向压扁目标比例，约 0.7 较自然。</summary>
    public float crouchSquashRatio = 0.7f;
    /// <summary>当前压扁插值，0~1。</summary>
    private float currentSquash = 1f;

    [Header("Color Sync (颜色主从同步)")]
    [Tooltip("必须把 Hair 物体拖到这里！")]
    public HairController hairMaster;
    /// <summary>无 hairMaster 时使用的 fallback 颜色。</summary>
    public Color fallbackColor = Color.white;

    [Header("Tail Visual Options")]
    [Tooltip("是否让尾巴比头发颜色稍深一点")]
    public bool useDarkenEffect = true;
    [Range(0f, 1f)]
    [Tooltip("加深程度：1为原色，数值越小越暗")]
    public float darkenMultiplier = 0.85f;

    [Header("Cat Tail Idle Shape (静止形状调节)")]
    [Tooltip("静止时尾巴向上翘的力度")]
    public float idleLift = 15f;
    [Tooltip("静止时尾巴向后方延展的力度")]
    public float idleSpread = 5f;
    [Tooltip("尾巴整体的柔软度，值越大越硬")]
    public float stiffness = 0.5f;

    /// <summary>父级玩家控制器，用于朝向、状态与速度。</summary>
    private PlayerController player;
    /// <summary>本物体 Mesh 过滤器。</summary>
    private MeshFilter meshFilter;
    /// <summary>本物体 Mesh 渲染器。</summary>
    private MeshRenderer meshRenderer;
    /// <summary>尾巴网格实例，每帧重算。</summary>
    private Mesh tailMesh;
    /// <summary>各段节点世界坐标，由 UpdatePositions 更新。</summary>
    private List<Vector2> nodePositions = new List<Vector2>();
    /// <summary>上一帧朝向，用于转身时镜像节点。</summary>
    private int lastFacingDirection;

    /// <summary>当前 Mesh 构建用顶点列表。</summary>
    private List<Vector3> vertices = new List<Vector3>();
    /// <summary>三角形索引。</summary>
    private List<int> triangles = new List<int>();
    /// <summary>UV 坐标。</summary>
    private List<Vector2> uvs = new List<Vector2>();
    /// <summary>顶点色。</summary>
    private List<Color> colors = new List<Color>();

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        tailMesh = new Mesh();
        tailMesh.name = "CatTailMesh";
        meshFilter.mesh = tailMesh;

        Shader urp2DShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Material mat = new Material(urp2DShader);

        if (circleSprite != null)
        {
            mat.mainTexture = circleSprite.texture;
        }
        meshRenderer.material = mat;

        meshRenderer.sortingLayerName = "Player";
        meshRenderer.sortingOrder = -2;

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        nodePositions.Clear();
        for (int i = 0; i < segmentCount; i++) nodePositions.Add(transform.position);
    }

    private void Start()
    {
        if (player != null) lastFacingDirection = player.FacingDirection;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        if (meshRenderer == null || !meshRenderer.enabled)
        {
            if (tailMesh != null) tailMesh.Clear();
            return;
        }

        // 1. 计算挤压
        float targetSquash = (player.StateMachine.CurrentState == player.CrouchState) ? crouchSquashRatio : 1f;
        currentSquash = Mathf.Lerp(currentSquash, targetSquash, Time.deltaTime * 15f);

        UpdatePositions();
        RenderTailMesh();
    }

    /// <summary>
    /// 根据当前锚点、朝向与速度更新尾巴链各节点；静止时使用 idleLift/idleSpread 微动。
    /// </summary>
    private void UpdatePositions()
    {
        Transform activeAnchor = player.FacingDirection > 0 ? tailAnchorRight : tailAnchorLeft;
        Vector2 currentAnchorPos = activeAnchor != null ? (Vector2)activeAnchor.position : (Vector2)transform.position;

        if (player.FacingDirection != lastFacingDirection)
        {
            for (int i = 1; i < nodePositions.Count; i++)
            {
                float relativeX = nodePositions[i].x - nodePositions[0].x;
                nodePositions[i] = new Vector2(currentAnchorPos.x - relativeX, nodePositions[i].y);
            }
        }
        lastFacingDirection = player.FacingDirection;

        nodePositions[0] = currentAnchorPos;

        for (int i = 1; i < segmentCount; i++)
        {
            Vector2 force;
            bool isMoving = Mathf.Abs(player.RB.linearVelocity.x) > 0.1f || Mathf.Abs(player.RB.linearVelocity.y) > 0.1f;

            if (isMoving)
            {
                force = new Vector2(-player.FacingDirection * drag, -gravity);
            }
            else
            {
                force = new Vector2(-player.FacingDirection * idleSpread, idleLift * (1.2f - (i * 0.1f)));
                force.y += Mathf.Sin(Time.time * 2f + i) * 1.5f;
                force.x += Mathf.Cos(Time.time * 1.5f + i) * 1f;
            }

            Vector2 target = nodePositions[i - 1] + force * Time.deltaTime;
            nodePositions[i] = Vector2.Lerp(nodePositions[i], target, Time.deltaTime * followSpeed);

            float d = Vector2.Distance(nodePositions[i], nodePositions[i - 1]);
            if (d > nodeDistance)
                nodePositions[i] = nodePositions[i - 1] + (nodePositions[i] - nodePositions[i - 1]).normalized * nodeDistance;
        }
    }

    /// <summary>
    /// 清空缓冲，先描边再主色两遍绘制，应用压扁与颜色后写入 tailMesh。
    /// </summary>
    private void RenderTailMesh()
    {
        vertices.Clear(); triangles.Clear(); uvs.Clear(); colors.Clear();

        // 1. 获取颜色
        Color rawSyncColor = (hairMaster != null) ? hairMaster.CurrentHairColor : fallbackColor;

        // 2. 应用加深效果
        Color processedColor = rawSyncColor;
        if (useDarkenEffect)
        {
            processedColor.r *= darkenMultiplier;
            processedColor.g *= darkenMultiplier;
            processedColor.b *= darkenMultiplier;
        }

        // 3. 统一处理线性空间
        Color finalColorForMesh = processedColor;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            finalColorForMesh = processedColor.linear;
        }

        // 4. 渲染 Pass 1: 黑边
        for (int i = 0; i < segmentCount; i++)
        {
            float r = baseRadius * Mathf.Pow(tapering, i);
            // 这里可以加上你之前做好的身体缩放补偿 (characterScale)
            AddQuad(nodePositions[i], r + 0.125f, Color.black);
        }

        // 5. 渲染 Pass 2: 彩色核心
        for (int i = 0; i < segmentCount; i++)
        {
            float r = baseRadius * Mathf.Pow(tapering, i);
            // 传入已经校正好的颜色
            AddQuad(nodePositions[i], r, finalColorForMesh);
        }

        // 6. 应用到网格
        tailMesh.Clear();
        tailMesh.vertices = vertices.ToArray();
        tailMesh.uv = uvs.ToArray();
        tailMesh.triangles = triangles.ToArray();
        tailMesh.colors = colors.ToArray();
    }

    /// <summary>
    /// 在指定世界位置添加一个椭圆段（压扁补偿），追加顶点/UV/三角形/顶点色到当前缓冲。
    /// </summary>
    /// <param name="worldPos">段中心世界坐标</param>
    /// <param name="radius">基础半径</param>
    /// <param name="color">顶点色</param>
    private void AddQuad(Vector2 worldPos, float radius, Color color)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int vIndex = vertices.Count;

        // 【新注入点】应用挤压到半径上
        float squashedRadiusY = radius * currentSquash;
        float bulgedRadiusX = radius * (1f + (1f - currentSquash) * 0.3f); // 稍微变宽补偿体积

        // 使用不同的 X 和 Y 半径来生成顶点，这就变成了椭圆(压扁的圆)
        vertices.Add(localPos + new Vector3(-bulgedRadiusX, -squashedRadiusY, 0));
        vertices.Add(localPos + new Vector3(bulgedRadiusX, -squashedRadiusY, 0));
        vertices.Add(localPos + new Vector3(-bulgedRadiusX, squashedRadiusY, 0));
        vertices.Add(localPos + new Vector3(bulgedRadiusX, squashedRadiusY, 0));

        //vertices.Add(localPos + new Vector3(-radius, -radius, 0));
        //vertices.Add(localPos + new Vector3(radius, -radius, 0));
        //vertices.Add(localPos + new Vector3(-radius, radius, 0));
        //vertices.Add(localPos + new Vector3(radius, radius, 0));

        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));

        triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 1);
        triangles.Add(vIndex + 1); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);

        // 直接添加传入的颜色
        for (int j = 0; j < 4; j++) colors.Add(color);
    }
}
