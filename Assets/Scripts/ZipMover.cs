using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class ZipMover : MonoBehaviour, IVelocityProvider
{
    // ==========================================
    // 1. 基础运动参数
    // ==========================================
    [Header("1. Movement Logic")]
    public Vector2 targetOffset = new Vector2(0, 10);
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public float dashDuration = 0.5f;
    public float returnDuration = 2.0f;
    public float windUpTime = 0.5f;
    public float stayTime = 0.5f;

    // --- 【新增】激活模式选项 ---
    public enum ActivationMode
    {
        TopOnly,    // 经典模式：只有踩在上面才激活
        AnyTouch    // 触碰模式：碰到侧面、底部都能激活
    }
    [Tooltip("TopOnly: 只有踩头激活; AnyTouch: 碰到任何一面都激活")]
    public ActivationMode activationMode = ActivationMode.TopOnly;

    // ==========================================
    // 2. 交通灯系统
    // ==========================================
    [Header("2. Traffic Lights System")]
    public SpriteRenderer[] trafficSprites;
    public Light2D[] trafficLights;

    [ColorUsage(true, true)] public Color redOnColor = new Color(1, 0.1f, 0.1f) * 2f;
    [ColorUsage(true, true)] public Color amberOnColor = new Color(1, 0.6f, 0.1f) * 2f;
    [ColorUsage(true, true)] public Color greenOnColor = new Color(0.1f, 1f, 0.1f) * 2f;
    public float lightScaleMultiplier = 1.1f;

    // ==========================================
    // 3. 视觉反馈
    // ==========================================
    [Header("3. Visuals & Feedback")]
    public Transform visualRoot;
    public SpriteRenderer frameSprite;
    public float impactShakeAmount = 0.3f;
    public LayerMask playerLayer;
    public SoundData touchSFX;   // 对应 a_touch (启动感应)
    public SoundData impactSFX;  // 对应 b_impact (撞击终点)
    public SoundData returnSFX;  // 对应 c_return (返回时的机械Loop)
    public SoundData resetSFX;   // 对应 d_reset (回到起点锁定)

    // ==========================================
    // 4. 危险设置
    // ==========================================
    [Header("4. Danger Settings")]
    public DeathStrategy crushStrategy;

    // ==========================================
    // 5. 齿轮设置 (变速核心)
    // ==========================================
    [Header("5. Conveyor Gears")]
    public Transform startGear;
    public Transform endGear;
    [Tooltip("前进时的标准旋转速度")]
    public float gearRotationSpeed = 360f;

    [Tooltip("返回时的转速比率 (1.0 = 和前进一样快; 0.5 = 慢一半)")]
    [Range(0.1f, 1.0f)]
    public float returnGearRatio = 1.0f;

    // ==========================================
    // 6. 链条贴图
    // ==========================================
    [Header("6. Chain Sprites (Randomized)")]
    public Sprite[] topChainSprites;
    public Sprite[] bottomChainSprites;

    public float chainSpacing = 0.6f;
    public float scrollSpeedMultiplier = 1.0f;
    public float linkRotationOffset = 0f;

    // ==========================================
    // 7. 链条渲染层级配置
    // ==========================================
    [Header("7. Chain Layer Settings")]
    public string chainSortingLayerName = "Default";
    public int chainSortingOrder = -10;

    // ==========================================
    // 8. 特效系统 (火花)
    // ==========================================
    [Header("8. Effects")]
    [Tooltip("起点齿轮的火花粒子系统")]
    public ParticleSystem startGearSparks;
    [Tooltip("终点齿轮的火花粒子系统")]
    public ParticleSystem endGearSparks;

    // 9. 对齐顿挫动画 (Snap Animation)
    [Header("9. Snap/Alignment Animation")]
    [Tooltip("对齐动画的持续时间")]
    public float snapDuration = 0.2f;
    [Tooltip("视觉偏移的强度 (过冲距离)")]
    public float snapAmount = 0.15f;
    [Tooltip("对齐曲线：建议设为 (0,0) -> (0.3, 1) -> (1, 0) 的形状，模拟撞击后的回弹")]
    public AnimationCurve snapCurve = new AnimationCurve(
        new Keyframe(0, 0),
        new Keyframe(0.3f, 1f), // 瞬间冲出去
        new Keyframe(0.6f, -0.2f), // 稍微回弹一点点
        new Keyframe(1f, 0) // 归位
    );

    // --- 内部变量 ---
    private Vector2 _startPos;
    private Vector2 _targetPos;
    private Vector2 _currentVelocity;
    private Rigidbody2D _rb;
    private BoxCollider2D _col;
    private bool _isTriggered = false;
    private AudioSource _audioSource;
    private HashSet<IMovingPlatformRider> _riders = new HashSet<IMovingPlatformRider>();

    private Color[] _defaultSpriteColors;
    private float[] _defaultLightOuterRadius;
    private float _startGearZ;
    private float _endGearZ;

    // --- 链条对象池 ---
    private Transform _chainRoot;
    private List<(Transform trans, SpriteRenderer sr)> _topLinks = new List<(Transform, SpriteRenderer)>();
    private List<(Transform trans, SpriteRenderer sr)> _bottomLinks = new List<(Transform, SpriteRenderer)>();

    private Vector2 _pathDir;
    private float _pathLength;
    private float _linkLength;
    private bool _isReturning = false;

    private Coroutine _currentSnapRoutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<BoxCollider2D>();
        _audioSource = GetComponent<AudioSource>();
        if (!_audioSource) _audioSource = gameObject.AddComponent<AudioSource>();

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _startPos = transform.position;
        _targetPos = _startPos + targetOffset;

        if (startGear) _startGearZ = startGear.position.z;
        if (endGear) _endGearZ = endGear.position.z;

        InitVisuals();

        if (topChainSprites != null && topChainSprites.Length > 0 &&
            bottomChainSprites != null && bottomChainSprites.Length > 0)
        {
            BuildChains();
        }

        StopSparks();
        SetTrafficLight(0);
    }

    private void BuildChains()
    {
        if (_chainRoot) Destroy(_chainRoot.gameObject);
        GameObject rootObj = new GameObject("Chain_Visuals_Root");
        rootObj.transform.SetParent(transform);
        rootObj.transform.localPosition = Vector3.zero;
        _chainRoot = rootObj.transform;

        _pathDir = (_targetPos - _startPos).normalized;
        _pathLength = Vector2.Distance(_startPos, _targetPos);
        _linkLength = topChainSprites[0].bounds.size.x;

        if (_linkLength <= 0.001f) _linkLength = 1f;

        int count = Mathf.CeilToInt(_pathLength / _linkLength) + 2;

        float angle = Mathf.Atan2(_pathDir.y, _pathDir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, angle + linkRotationOffset);

        _topLinks = CreateChainList(count, topChainSprites, "TopLink", rot);
        _bottomLinks = CreateChainList(count, bottomChainSprites, "BottomLink", rot);
    }

    private List<(Transform, SpriteRenderer)> CreateChainList(int count, Sprite[] sprites, string prefix, Quaternion rotation)
    {
        var list = new List<(Transform, SpriteRenderer)>();
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"{prefix}_{i}");
            go.transform.SetParent(_chainRoot);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[Random.Range(0, sprites.Length)];
            sr.sortingLayerName = chainSortingLayerName;
            sr.sortingOrder = chainSortingOrder;

            go.transform.rotation = rotation;
            list.Add((go.transform, sr));
        }
        return list;
    }

    private void InitVisuals()
    {
        if (frameSprite != null && trafficSprites != null)
        {
            int baseOrder = frameSprite.sortingOrder;
            foreach (var sr in trafficSprites)
            {
                if (sr != null)
                {
                    sr.sortingLayerID = frameSprite.sortingLayerID;
                    sr.sortingOrder = Mathf.Max(sr.sortingOrder, baseOrder + 1);
                }
            }
        }
        if (trafficSprites != null)
        {
            _defaultSpriteColors = new Color[trafficSprites.Length];
            for (int i = 0; i < trafficSprites.Length; i++)
                if (trafficSprites[i]) _defaultSpriteColors[i] = trafficSprites[i].color;
        }
        if (trafficLights != null)
        {
            _defaultLightOuterRadius = new float[trafficLights.Length];
            for (int i = 0; i < trafficLights.Length; i++)
                if (trafficLights[i]) _defaultLightOuterRadius[i] = trafficLights[i].pointLightOuterRadius;
        }
    }

    private void FixedUpdate()
    {
        if (!_isTriggered) CheckForTrigger();
    }

    // ==========================================
    // 【修改】激活检测逻辑
    // ==========================================
    private void CheckForTrigger()
    {
        bool shouldTrigger = false;

        if (activationMode == ActivationMode.TopOnly)
        {
            // --- 模式A：只踩头激活 ---
            float checkHeight = 0.1f;
            RaycastHit2D hit = Physics2D.BoxCast(
                _col.bounds.center + Vector3.up * (_col.bounds.extents.y + checkHeight / 2),
                new Vector2(_col.bounds.size.x - 0.05f, checkHeight),
                0f, Vector2.up, 0f, playerLayer);

            if (hit.collider != null) shouldTrigger = true;
        }
        else
        {
            // --- 模式B：碰触任意面激活 ---
            // 检测 Collider 周围稍微扩大一点点的范围是否有玩家
            Collider2D hit = Physics2D.OverlapBox(
                _col.bounds.center,
                _col.bounds.size + new Vector3(0.05f, 0.05f, 0), // 上下左右各扩大一点
                0f, playerLayer);

            if (hit != null) shouldTrigger = true;
        }

        if (shouldTrigger) StartCoroutine(SequenceRoutine());
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!_isTriggered) return;

        // -------------------------------------------------------------
        // 【已修改】注释掉了下面的撞击死亡代码
        // Celeste 逻辑：移动平台本身不杀人，只有把人挤压在墙上时(Crush)才会死。
        // -------------------------------------------------------------
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player != null && !player.IsDead)
        {
            Vector2 normal = collision.GetContact(0).normal;
            float dot = Vector2.Dot(_currentVelocity.normalized, normal);
            if (_currentVelocity.magnitude > 1f && dot < -0.3f)
                player.Die(_currentVelocity.normalized, crushStrategy);
        }
        
    }

    private IEnumerator SequenceRoutine()
    {
        _isTriggered = true;
        _isReturning = false;

        // === 【A. 触发音效】 ===
        AudioManager.Instance.PlaySFX(touchSFX, transform.position);

        // 1. 蓄力 (Wind Up)
        StopSparks();
        SetTrafficLight(1);
        float timer = 0f;
        Vector2 visualLocalStart = visualRoot ? visualRoot.localPosition : Vector2.zero;

        while (timer < windUpTime)
        {
            timer += Time.fixedDeltaTime;
            // 交通灯闪烁逻辑保持不变...
            if (timer % 0.15f < 0.075f) SetTrafficLight(0); else SetTrafficLight(1);

            if (visualRoot)
                visualRoot.localPosition = visualLocalStart + (Vector2)Random.insideUnitCircle * ((timer / windUpTime) * 0.08f);
            yield return new WaitForFixedUpdate();
        }
        if (visualRoot) visualRoot.localPosition = visualLocalStart;

        // 2. 冲刺 (Dash)
        SetTrafficLight(2);
        PlaySparks();
        // 冲刺通常极快，我们可以选择不播Loop或者播一个高频Loop
        yield return MovePlatform(_startPos, _targetPos, dashDuration, dashCurve);

        // === 【B. 撞击终点音效】 ===
        StopSparks();
        AudioManager.Instance.PlaySFX(impactSFX, transform.position);

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player) player.StartShake(targetOffset.normalized * impactShakeAmount);
        TriggerSnapAnimation(_pathDir);

        SetTrafficLight(0);
        yield return new WaitForSeconds(stayTime);

        // 3. 返回 (Return)
        SetTrafficLight(1);
        _isReturning = true;

        // === 【C. 返回循环音效】 ===
        // 只有返回阶段使用循环机械音，因为返回通常比较慢
        StartZipLoop(returnSFX);
        yield return MovePlatform(_targetPos, _startPos, returnDuration, returnCurve);
        StopZipLoop();

        _isReturning = false;

        // === 【D. 重置锁定音效】 ===
        AudioManager.Instance.PlaySFX(resetSFX, transform.position);
        TriggerSnapAnimation(-_pathDir);

        SetTrafficLight(0);
        _isTriggered = false;
    }

    private IEnumerator MovePlatform(Vector2 from, Vector2 to, float duration, AnimationCurve curve)
    {
        float t = 0f;
        Vector2 lastPos = _rb.position;

        while (t < 1f)
        {
            t += Time.fixedDeltaTime / duration;
            if (t > 1f) t = 1f;

            float curveVal = curve.Evaluate(t);
            Vector2 nextPos = Vector2.Lerp(from, to, curveVal);
            Vector2 frameDelta = nextPos - lastPos;

            if (Time.fixedDeltaTime > 0)
                _currentVelocity = frameDelta / Time.fixedDeltaTime;

            MovePassengers(frameDelta);
            _rb.MovePosition(nextPos);
            lastPos = nextPos;

            yield return new WaitForFixedUpdate();
        }
        _currentVelocity = Vector2.zero;
    }

    private void MovePassengers(Vector2 moveAmount)
    {
        _riders.Clear();
        float checkWidth = _col.bounds.size.x + 0.05f;
        float checkHeight = 0.2f;

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            _col.bounds.center + Vector3.up * (_col.bounds.extents.y + checkHeight / 2),
            new Vector2(checkWidth, checkHeight),
            0f, Vector2.up, 0f, playerLayer);

        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;
            IMovingPlatformRider rider = hit.transform.GetComponent<IMovingPlatformRider>();
            var pc = hit.transform.GetComponent<PlayerController>();
            if (pc != null && pc.IsDead) continue;
            if (rider != null && !_riders.Contains(rider))
            {
                rider.ManualMove(moveAmount);
                _riders.Add(rider);
            }
        }
    }

    private void SetTrafficLight(int activeIndex)
    {
        for (int i = 0; i < trafficSprites.Length; i++)
        {
            bool isActive = (i == activeIndex);
            Color targetCol;
            if (isActive)
            {
                if (i == 0) targetCol = redOnColor;
                else if (i == 1) targetCol = amberOnColor;
                else targetCol = greenOnColor;
            }
            else
            {
                targetCol = _defaultSpriteColors != null && i < _defaultSpriteColors.Length
                            ? _defaultSpriteColors[i] : Color.white;
            }

            if (trafficSprites[i]) trafficSprites[i].color = targetCol;

            if (trafficLights != null && i < trafficLights.Length && trafficLights[i])
            {
                trafficLights[i].enabled = true;
                trafficLights[i].color = targetCol;
                float targetRadius = isActive ? _defaultLightOuterRadius[i] * lightScaleMultiplier : 0f;
                trafficLights[i].intensity = isActive ? 1.0f : 0f;
                trafficLights[i].pointLightOuterRadius = targetRadius;
            }
        }
    }

    public Vector2 GetVelocityAtPoint(Vector3 worldPoint) => _currentVelocity;

    private void LateUpdate()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        float currentDistFromStart = Vector2.Distance(_startPos, transform.position);
        float effectiveVisualDist;

        if (!_isReturning)
        {
            effectiveVisualDist = currentDistFromStart;
        }
        else
        {
            float distTraveledBack = _pathLength - currentDistFromStart;
            float visualDistBack = distTraveledBack * returnGearRatio;
            effectiveVisualDist = _pathLength - visualDistBack;
            if (!_isTriggered) effectiveVisualDist = 0;
        }

        float angle = effectiveVisualDist * gearRotationSpeed;

        if (startGear)
        {
            startGear.position = new Vector3(_startPos.x, _startPos.y, _startGearZ);
            startGear.rotation = Quaternion.Euler(0, 0, angle);

            if (startGearSparks)
            {
                startGearSparks.transform.position = startGear.position;
                startGearSparks.transform.rotation = Quaternion.identity;
            }
        }

        if (endGear)
        {
            endGear.position = new Vector3(_targetPos.x, _targetPos.y, _endGearZ);
            endGear.rotation = Quaternion.Euler(0, 0, angle);

            if (endGearSparks)
            {
                endGearSparks.transform.position = endGear.position;
                endGearSparks.transform.rotation = Quaternion.identity;
            }
        }

        if (_topLinks.Count > 0 && _linkLength > 0)
        {
            Vector2 perp = Vector2.Perpendicular(_pathDir) * (chainSpacing / 2f);
            float scrollOffset = (effectiveVisualDist * scrollSpeedMultiplier) % _linkLength;
            UpdateChainList(_topLinks, perp, -scrollOffset);
            UpdateChainList(_bottomLinks, -perp, scrollOffset);
        }
    }

    private void UpdateChainList(List<(Transform trans, SpriteRenderer sr)> links, Vector2 offsetPerp, float scrollOffset)
    {
        float loopLength = links.Count * _linkLength;
        for (int i = 0; i < links.Count; i++)
        {
            float distanceOnLine = (i * _linkLength) + scrollOffset;
            while (distanceOnLine < 0) distanceOnLine += loopLength;
            while (distanceOnLine >= loopLength) distanceOnLine -= loopLength;

            bool isVisible = distanceOnLine <= _pathLength + 0.01f;
            if (links[i].sr.enabled != isVisible) links[i].sr.enabled = isVisible;

            if (isVisible)
            {
                Vector2 finalPos2D = _startPos + offsetPerp + _pathDir * distanceOnLine;
                float z = transform.position.z + 1;
                links[i].trans.position = new Vector3(finalPos2D.x, finalPos2D.y, z);
            }
        }
    }

    private void PlaySound(AudioClip clip, float vol) { if (clip && _audioSource) _audioSource.PlayOneShot(clip, vol); }

    private void PlaySparks()
    {
        if (startGearSparks) startGearSparks.Play();
        if (endGearSparks) endGearSparks.Play();
    }

    private void StopSparks()
    {
        if (startGearSparks) startGearSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (endGearSparks) endGearSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void TriggerSnapAnimation(Vector2 direction)
    {
        if (visualRoot == null) return;
        if (_currentSnapRoutine != null) StopCoroutine(_currentSnapRoutine);
        _currentSnapRoutine = StartCoroutine(AnimateSnapRoutine(direction));
    }

    private IEnumerator AnimateSnapRoutine(Vector2 dir)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / snapDuration;
            float curveValue = snapCurve.Evaluate(t);

            // 计算偏移：方向 * 强度 * 曲线值
            // 这会让 visualRoot 瞬间偏离(过冲)，然后被曲线拉回来
            visualRoot.localPosition = dir * curveValue * snapAmount;

            yield return null;
        }
        visualRoot.localPosition = Vector3.zero;
        _currentSnapRoutine = null;
    }

    private void StartZipLoop(SoundData data)
    {
        if (data != null && _audioSource != null)
        {
            _audioSource.clip = data.clip;
            _audioSource.loop = true; // 确保是循环的
            _audioSource.volume = data.volume * AudioManager.Instance.sfxVolume * AudioManager.Instance.masterVolume;
            _audioSource.pitch = Random.Range(data.minPitch, data.maxPitch);
            _audioSource.Play();
        }
    }

    private void StopZipLoop()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
}