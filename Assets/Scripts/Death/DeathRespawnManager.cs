using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 死亡-重生流程编排：订阅玩家死亡事件，按四阶段调用策略，在存档点复活。
/// 持有重生点、转场 UI、音源与玩家引用，不依赖 GameManager。
/// </summary>
public class DeathRespawnManager : MonoBehaviour, IDeathRespawnContext
{
    public static DeathRespawnManager Instance { get; private set; }

    #region IDeathRespawnContext
    public Image TransitionImage => transitionImage;
    public AudioSource AudioSource => audioSource;
    public PlayerController Player => player;
    #endregion

    #region Inspector
    [Header("Respawn")]
    [SerializeField] private Vector2 respawnPoint;
    [SerializeField] private Transform defaultSpawnPoint;

    [Header("Transition")]
    [SerializeField] private Image transitionImage;
    [SerializeField] private AudioSource audioSource;

    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Death Strategy")]
    [SerializeField] private DeathStrategy defaultDeathStrategy;
    #endregion

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (defaultSpawnPoint != null)
            respawnPoint = defaultSpawnPoint.position;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (transitionImage != null)
        {
            transitionImage.enabled = false;
            transitionImage.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        PlayerController.OnPlayerDied += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        PlayerController.OnPlayerDied -= HandlePlayerDeath;
    }

    public void UpdateRespawnPoint(Vector2 newPos)
    {
        respawnPoint = newPos;
        Debug.Log("DeathRespawnManager: 存档重生点为 " + newPos);
    }

    public Vector2 GetRespawnPoint() => respawnPoint;

    private void HandlePlayerDeath(PlayerController playerRef, Vector3 hitDir, DeathStrategy strategyOverride)
    {
        DeathStrategy finalStrategy = strategyOverride != null ? strategyOverride : defaultDeathStrategy;
        StartCoroutine(DeathFlow(playerRef, hitDir, finalStrategy));
    }

    private IEnumerator DeathFlow(PlayerController playerRef, Vector3 hitDir, DeathStrategy activeStrategy)
    {
        if (activeStrategy == null)
        {
            Debug.LogError("DeathRespawnManager：没有可执行的 DeathStrategy");
            yield return new WaitForSeconds(1f);
            if (playerRef != null)
            {
                playerRef.transform.position = respawnPoint;
                playerRef.ReviveInternal();
            }
            yield break;
        }

        yield return StartCoroutine(activeStrategy.ExecuteDeath(playerRef, hitDir));
        yield return StartCoroutine(activeStrategy.TransitionIn(this));

        if (playerRef != null)
        {
            Rigidbody2D rb = playerRef.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }
            playerRef.transform.position = respawnPoint;
        }

        yield return StartCoroutine(activeStrategy.ExecuteRespawn(playerRef, respawnPoint));
        yield return StartCoroutine(activeStrategy.TransitionOut(this));
    }
}
