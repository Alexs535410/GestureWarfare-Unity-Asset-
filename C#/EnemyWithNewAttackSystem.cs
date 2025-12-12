using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 使用新攻击系统的敌人类
/// 集成了状态机和独立攻击物体系统
/// </summary>
public class EnemyWithNewAttackSystem : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    
    [Header("Animation System")]
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private AnimationClip appearAnimation;
    [SerializeField] private AnimationClip attackPreparationAnimation;
    [SerializeField] private AnimationClip attackAnimation;
    [SerializeField] private AnimationClip idleAnimation;
    [SerializeField] private AnimationClip moveAnimation;
    [SerializeField] private AnimationClip deadAnimation;
    
    [Header("Movement Settings")]
    [SerializeField] private GameObject target; // 攻击目标
    [SerializeField] private bool isMovingInXOY = true;
    [SerializeField] private float xoyMoveSpeed = 30f;
    [SerializeField] private MovementPattern xoyMovementPattern = MovementPattern.Random;
    [SerializeField] private float randomMoveRadius = 10f;
    [SerializeField] private float randomMoveInterval = 3f;
    
    [Header("Screen Entry Settings")]
    [SerializeField] private bool enableScreenEntryCheck = true; // 是否启用屏幕内检测
    [SerializeField] private float screenEntrySpeed = 70f; // 移动到屏幕内的速度
    [SerializeField] private float screenMargin = 100f; // 屏幕边缘的安全距离
    [SerializeField] private bool hasEnteredScreen = false; // 是否已经进入过屏幕
    
    [Header("Rotation Settings")]
    [SerializeField] private bool alwaysFacePlayer = false; // 是否始终朝向玩家
    [SerializeField] private Vector3 forwardDirection = Vector3.right; // 敌人的正方向（默认向右）
    [SerializeField] private float rotationSpeed = 5f; // 旋转速度（平滑旋转）
    [SerializeField] private bool instantRotation = false; // 是否瞬间旋转（false为平滑旋转）
    
    [Header("New Attack System")]
    [SerializeField] private bool enableNewAttackSystem = true;
    
    [Header("Body Parts")]
    [SerializeField] private List<BodyPart> bodyParts = new List<BodyPart>();
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject bloodEffectPrefab;
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.1f;
    
    // 组件引用
    private EnemyStateMachine stateMachine;
    private Camera playerCamera;
    
    // 护甲伤害倍率
    private Dictionary<ArmorType, float> armorDamageMultiplier = new Dictionary<ArmorType, float>()
    {
        { ArmorType.WeakPoint, 1.5f },
        { ArmorType.Light, 1.0f },
        { ArmorType.Medium, 0.8f },
        { ArmorType.Heavy, 0.5f },
        { ArmorType.Unattackable, 0.0f }
    };
    
    // 移动相关变量
    private float currentDistanceToTarget;
    private Vector3 randomTargetPosition;
    private float randomMoveTimer;
    private Vector3 basicSpawnPosition;
    
    // 屏幕进入相关变量
    private Vector3 screenEntryTarget; // 屏幕内目标位置
    private bool isMovingToScreen = false; // 是否正在移动到屏幕内
    
    // 动画相关变量
    private bool isPlayingAnimation = false;
    private string currentAnimationState = "Idle";
    
    // 事件
    public System.Action<EnemyWithNewAttackSystem> OnEnemyDeath;
    public System.Action<EnemyWithNewAttackSystem, float> OnEnemyDamaged;
    public System.Action<BodyPart> OnBodyPartDestroyed;
    public System.Action<EnemyWithNewAttackSystem> OnEnemyEnteredScreen; // 敌人进入屏幕事件
    
    // 属性
    public bool IsDead { get; private set; } = false;
    public bool HasEnteredScreen { get => hasEnteredScreen; }

    [Header("Attack Preparation Settings")]
    private bool hasPlayedAppearAnimation = false;
    [SerializeField] private float appearAnimationDuration = 2f; // appear动画持续时间
    [SerializeField] private float attackPreparationDamageThreshold = 50f; // 攻击准备状态伤害阈值
    private float accumulatedDamageInPreparation = 0f; // 攻击准备期间累积伤害

    [Header("Score Settings")]
    [SerializeField] private int scoreValue = 10; // 敌人分数
    
    // 获取分数
    public int GetScore()
    {
        return scoreValue;
    }
    
    // 设置分数
    public void SetScore(int score)
    {
        scoreValue = score;
    }

    private void Awake()
    {
        basicSpawnPosition = transform.position;
        currentHealth = maxHealth;
        InitializeComponents();
        SetupTarget();
        SetupCamera();
        InitializeMovement();
        InitializeAnimation();
        InitializeBodyParts();
    }
    
    private void Start()
    {
        SetupBodyPartComponents();
        
        // 创建血条
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.CreateHealthBar(gameObject, maxHealth, currentHealth);
        }
        
        // 延迟播放appear动画，确保状态机不会立即覆盖
        StartCoroutine(PlayAppearAnimationSequence());
        
        // 检查是否需要移动到屏幕内
        if (enableScreenEntryCheck && !hasEnteredScreen)
        {
            CheckAndMoveToScreen();
        }
    }
    
    private void Update()
    {
        if (!IsDead && target != null)
        {
            // 如果正在移动到屏幕内，优先处理屏幕进入逻辑
            if (isMovingToScreen)
            {
                HandleScreenEntry();
            }
            else
            {
                // 只有在进入屏幕后才执行正常的移动逻辑
                if (hasEnteredScreen)
                {
                    UpdateDistanceToTarget();
                    HandleMovement();
                }
            }
            
            // 处理朝向玩家的逻辑
            if (alwaysFacePlayer && hasEnteredScreen)
            {
                HandleRotationTowardsPlayer();
            }
        }
    }
    
    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponents()
    {
        // 添加状态机组件
        if (enableNewAttackSystem)
        {
            stateMachine = gameObject.GetComponent<EnemyStateMachine>();
            if (stateMachine == null)
            {
                stateMachine = gameObject.AddComponent<EnemyStateMachine>();
            }
            
            // 设置攻击配置
            /*
            if (attackConfigs.Count > 0)
            {
                // 这里可以通过反射或公共方法设置状态机的攻击配置
                // stateMachine.SetAttackConfigs(attackConfigs);
            }*/
        }
    }
    
    /// <summary>
    /// 设置目标
    /// </summary>
    private void SetupTarget()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player;
                Debug.Log($"Enemy {name} found target: {target.name}");
            }
            else
            {
                Debug.LogWarning($"Enemy {name} could not find player target!");
            }
        }
    }
    
    /// <summary>
    /// 设置摄像头
    /// </summary>
    private void SetupCamera()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<Camera>();
            }
        }
        
        if (playerCamera == null)
        {
            Debug.LogWarning($"Enemy {name} could not find camera!");
        }
    }
    
    /// <summary>
    /// 初始化移动
    /// </summary>
    private void InitializeMovement()
    {
        if (xoyMovementPattern == MovementPattern.Random)
        {
            SetNewRandomTarget();
        }
    }
    
    /// <summary>
    /// 初始化动画
    /// </summary>
    private void InitializeAnimation()
    {
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponent<Animator>();
            if (enemyAnimator == null)
            {
                enemyAnimator = gameObject.AddComponent<Animator>();
            }
        }
    }
    
    /// <summary>
    /// 初始化身体部位
    /// </summary>
    private void InitializeBodyParts()
    {
        if (bodyParts.Count == 0)
        {
            AutoDetectBodyParts();
        }
        
        foreach (var bodyPart in bodyParts)
        {
            if (bodyPart.partObject != null && bodyPart.partCollider == null)
            {
                var collider = bodyPart.partObject.GetComponent<Collider2D>();
                if (collider == null)
                {
                    collider = bodyPart.partObject.AddComponent<CircleCollider2D>();
                }
                bodyPart.partCollider = collider;
                bodyPart.partObject.tag = "Enemy";
            }
        }
    }
    
    /// <summary>
    /// 自动检测身体部位
    /// </summary>
    private void AutoDetectBodyParts()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            ArmorType defaultArmor = ArmorType.Light;
            float defaultMultiplier = 1f;
            bool enableDestruction = false;
            float partHealth = 100f;
            
            string childName = child.name.ToLower();
            if (childName.Contains("head"))
            {
                defaultArmor = ArmorType.WeakPoint;
                defaultMultiplier = 2f;
                enableDestruction = true;
                partHealth = 50f;
            }
            else if (childName.Contains("chest") || childName.Contains("body"))
            {
                defaultArmor = ArmorType.Medium;
                defaultMultiplier = 1f;
                enableDestruction = true;
                partHealth = 100f;
            }
            else if (childName.Contains("arm") || childName.Contains("leg"))
            {
                defaultArmor = ArmorType.Light;
                defaultMultiplier = 0.8f;
                enableDestruction = true;
                partHealth = 80f;
            }
            
            bodyParts.Add(new BodyPart(child.name, child.gameObject, defaultArmor, defaultMultiplier, enableDestruction, partHealth));
        }
    }
    
    /// <summary>
    /// 设置身体部位组件
    /// </summary>
    private void SetupBodyPartComponents()
    {
        foreach (var bodyPart in bodyParts)
        {
            if (bodyPart.partObject != null)
            {
                var bodyPartComponent = bodyPart.partObject.GetComponent<EnemyBodyPart>();
                if (bodyPartComponent == null)
                {
                    bodyPartComponent = bodyPart.partObject.AddComponent<EnemyBodyPart>();
                }
                
                // 这里需要适配新的Enemy类型
                bodyPartComponent.Initialize(this, bodyPart);
            }
        }
    }
    
    /// <summary>
    /// 检查并移动到屏幕内
    /// </summary>
    private void CheckAndMoveToScreen()
    {
        if (playerCamera == null) return;
        
        Vector3 screenPos = playerCamera.WorldToScreenPoint(transform.position);
        
        // 检查是否在屏幕内（考虑安全距离）
        bool isInScreen = IsPositionInScreen(screenPos);
        
        if (!isInScreen)
        {
            // 计算屏幕内的目标位置
            screenEntryTarget = CalculateScreenEntryPosition();
            isMovingToScreen = true;
            
            //Debug.Log($"Enemy {name} is outside screen, moving to screen position: {screenEntryTarget}");
        }
        else
        {
            // 已经在屏幕内，直接标记为已进入
            hasEnteredScreen = true;
            OnEnemyEnteredScreen?.Invoke(this);
            //Debug.Log($"Enemy {name} is already in screen");
        }
    }
    
    /// <summary>
    /// 检查位置是否在屏幕内
    /// </summary>
    /// <param name="screenPos">屏幕坐标</param>
    /// <returns>是否在屏幕内</returns>
    private bool IsPositionInScreen(Vector3 screenPos)
    {
        float margin = screenMargin * 10f; // 转换为像素单位的安全距离
        
        return screenPos.x >= margin && 
               screenPos.x <= Screen.width - margin && 
               screenPos.y >= margin && 
               screenPos.y <= Screen.height - margin && 
               screenPos.z > 0; // 确保在摄像头前方
    }
    
    /// <summary>
    /// 计算屏幕进入位置
    /// </summary>
    /// <returns>屏幕内的世界坐标</returns>
    private Vector3 CalculateScreenEntryPosition()
    {
        if (playerCamera == null) return transform.position;
        
        // 计算屏幕边缘的安全位置
        float margin = screenMargin;
        
        // 随机选择从哪一边进入屏幕
        int entrySide = Random.Range(0, 4);
        Vector2 screenEntryPoint = Vector2.zero;
        
        switch (entrySide)
        {
            case 0: // 从左边进入
                screenEntryPoint = new Vector2(Random.Range(margin, Screen.width / 2), Random.Range(margin, Screen.height - margin));
                break;
            case 1: // 从右边进入
                screenEntryPoint = new Vector2(Random.Range(Screen.width / 2, Screen.width - margin), Random.Range(margin, Screen.height - margin));
                break;
            case 2: // 从下边进入
                screenEntryPoint = new Vector2(Random.Range(margin, Screen.width - margin), Random.Range(margin, Screen.height - margin));
                break;
            case 3: // 从上边进入
                screenEntryPoint = new Vector2(Random.Range(margin, Screen.width - margin), Random.Range(margin, Screen.height - margin));
                break;
        }
        
        // 转换为世界坐标
        Vector3 worldPos = playerCamera.ScreenToWorldPoint(new Vector3(screenEntryPoint.x, screenEntryPoint.y, playerCamera.WorldToScreenPoint(transform.position).z));
        
        // 保持Z轴不变
        worldPos.z = transform.position.z;
        
        return worldPos;
    }
    
    /// <summary>
    /// 处理屏幕进入移动
    /// </summary>
    private void HandleScreenEntry()
    {
        if (!isMovingToScreen) return;
        
        // 移动到屏幕内目标位置
        Vector3 direction = (screenEntryTarget - transform.position).normalized;
        Vector3 movement = new Vector3(direction.x, direction.y, 0) * screenEntrySpeed * Time.deltaTime;
        
        transform.position += movement;
        
        // 播放移动动画
        if (!isPlayingAnimation)
        {
            PlayMoveAnimation();
        }
        
        // 检查是否到达目标位置
        float distanceToTarget = Vector3.Distance(transform.position, screenEntryTarget);
        if (distanceToTarget < 0.5f)
        {
            // 到达目标位置，完成屏幕进入
            isMovingToScreen = false;
            hasEnteredScreen = true;
            OnEnemyEnteredScreen?.Invoke(this);
            
            //Debug.Log($"Enemy {name} has entered screen at position: {transform.position}");
        }
    }
    
    /// <summary>
    /// 处理移动逻辑
    /// </summary>
    private void HandleMovement()
    {
        // 检查状态机是否允许移动
        bool canMove = true;
        if (stateMachine != null)
        {
            canMove = !stateMachine.IsAttacking();
        }

        if (canMove)
        {

            if (isMovingInXOY)
            {
                MoveInXOY();
            }
        }
        else 
        {
            Debug.Log(stateMachine.IsAttacking());
        }
    }
        
    /// <summary>
    /// XOY平面移动
    /// </summary>
    private void MoveInXOY()
    {
        switch (xoyMovementPattern)
        {
            case MovementPattern.Random:
                MoveRandomlyInXOY();
                break;
            case MovementPattern.Path:
                // 没写
                break;
        }
    }
    
    /// <summary>
    /// 随机移动
    /// </summary>
    private void MoveRandomlyInXOY()
    {
        randomMoveTimer += Time.deltaTime;
        
        if (Vector3.Distance(transform.position, randomTargetPosition) < 0.5f || randomMoveTimer >= randomMoveInterval)
        {
            SetNewRandomTarget();
        }
        
        Vector3 direction = (randomTargetPosition - transform.position).normalized;
        Vector3 xoyMovement = new Vector3(direction.x, direction.y, 0) * xoyMoveSpeed * Time.deltaTime;
        transform.position += xoyMovement;
        
        if (!isPlayingAnimation)
        {
            PlayMoveAnimation();
        }
    }
    
    /// <summary>
    /// 设置新的随机目标
    /// </summary>
    private void SetNewRandomTarget()
    {
        if (target == null) return;
        
        Vector2 randomCircle = Random.insideUnitCircle * randomMoveRadius;
        //randomTargetPosition = new Vector3(target.transform.position.x + randomCircle.x, target.transform.position.y + randomCircle.y, transform.position.z);

        randomTargetPosition = new Vector3(Random.Range(screenMargin, Screen.width - screenMargin), Random.Range(screenMargin, Screen.height - screenMargin), transform.position.z);

        randomMoveTimer = 0f;
    }
    
    /// <summary>
    /// 动画事件：随机瞬移
    /// 随机选择屏幕内的一个位置并立即瞬移到该位置
    /// </summary>
    public void AniEvent_RandomFlashMove()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning($"[{name}] AniEvent_RandomFlashMove: Camera is null, cannot perform flash move.");
            return;
        }
        
        // 生成屏幕内的随机位置（考虑安全距离）
        float margin = screenMargin;
        Vector2 randomScreenPos = new Vector2(
            Random.Range(margin, Screen.width - margin),
            Random.Range(margin, Screen.height - margin)
        );
        
        // 获取当前物体在屏幕中的深度
        Vector3 currentScreenPos = playerCamera.WorldToScreenPoint(transform.position);
        
        // 将屏幕坐标转换为世界坐标
        Vector3 worldPos = playerCamera.ScreenToWorldPoint(new Vector3(randomScreenPos.x, randomScreenPos.y, currentScreenPos.z));
        
        // 保持Z轴不变
        worldPos.z = transform.position.z;
        
        // 立即瞬移到目标位置
        transform.position = worldPos;
    }
    
    /// <summary>
    /// 更新到目标的距离
    /// </summary>
    private void UpdateDistanceToTarget()
    {
        if (target == null) return;
        // 注意此处enemy和player在同一平面上z相同
        currentDistanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        
    }
    
    /// <summary>
    /// 受到伤害（重写以支持攻击准备状态打断）
    /// </summary>
    /// <param name="baseDamage">基础伤害</param>
    /// <param name="hitPart">受击部位</param>
    public void TakeDamage(float baseDamage, BodyPart hitPart = null)
    {
        if (IsDead) return;
        
        float finalDamage = CalculateDamage(baseDamage, hitPart);
        
        if (finalDamage <= 0f) return;
        
        // 如果正在攻击准备状态，累积伤害
        if (stateMachine != null && (stateMachine.GetCurrentStateType() == EnemyStateType.AttackPreparation || stateMachine.GetCurrentStateType() == EnemyStateType.Attacking))
        {
            accumulatedDamageInPreparation += finalDamage;
            
            // 检查是否超过打断阈值
            if (accumulatedDamageInPreparation >= attackPreparationDamageThreshold)
            {
                stateMachine.InterruptAttackPreparation();
                accumulatedDamageInPreparation = 0f; // 重置累积伤害
                Debug.Log($"[{name}] Attack preparation interrupted by damage: {accumulatedDamageInPreparation}");
            }
        }
        
        currentHealth -= finalDamage;
        OnEnemyDamaged?.Invoke(this, finalDamage);
        
        // 更新血条
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.UpdateHealthBar(gameObject, currentHealth, maxHealth);
        }
        
        if (hitPart != null)
        {
            StartCoroutine(FlashBodyPart(hitPart));
            ShowBloodEffect(hitPart.partObject.transform.position);
            
            if (hitPart.enableDestruction)
            {
                HandleBodyPartDamage(hitPart, finalDamage);
            }
        }
        
        if (currentHealth <= 0 && !IsDead)
        {
            Die();
        }
        
        Debug.Log($"Enemy took {finalDamage:F1} damage. Health: {currentHealth:F1}/{maxHealth}");
    }
    
    /// <summary>
    /// 计算伤害
    /// </summary>
    /// <param name="baseDamage">基础伤害</param>
    /// <param name="hitPart">受击部位</param>
    /// <returns>最终伤害</returns>
    private float CalculateDamage(float baseDamage, BodyPart hitPart)
    {
        if (hitPart == null) return baseDamage;
        
        float armorMultiplier = armorDamageMultiplier[hitPart.armorType];
        return baseDamage * armorMultiplier * hitPart.damageMultiplier;
    }
    
    /// <summary>
    /// 处理身体部位伤害
    /// </summary>
    /// <param name="bodyPart">身体部位</param>
    /// <param name="damage">伤害值</param>
    private void HandleBodyPartDamage(BodyPart bodyPart, float damage)
    {
        bodyPart.partHealth -= damage;
        
        if (bodyPart.partHealth <= 0)
        {
            OnBodyPartDestroyed?.Invoke(bodyPart);
            Debug.Log($"Body part {bodyPart.partName} destroyed!");
        }
    }
    
    /// <summary>
    /// 死亡处理
    /// </summary>
    private void Die()
    {
        IsDead = true;
        OnEnemyDeath?.Invoke(this);
        
        // 移除血条
        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.RemoveHealthBar(gameObject);
        }
        
        // 中断所有攻击
        if (stateMachine != null)
        {
            stateMachine.InterruptAttack();
        }
        
        StartCoroutine(DeathSequence());
    }
    
    /// <summary>
    /// 死亡序列
    /// </summary>
    /// <returns></returns>
    private System.Collections.IEnumerator DeathSequence()
    {
        PlayDeadAnimation();
        
        yield return new WaitForSeconds(1f);
        
        Destroy(gameObject);
    }
    
    // 动画播放方法
    public void PlayAppearAnimation()
    {
        if (appearAnimation != null)
        {
            PlayAnimation(appearAnimation);
        }
    }
    
    public void PlayAttackPreparationAnimation()
    {
        if (attackPreparationAnimation != null)
        {
            PlayAnimation(attackPreparationAnimation);
        }
    }
    
    public void PlayAttackAnimation()
    {
        if (attackAnimation != null)
        {
            PlayAnimation(attackAnimation);
        }
    }
    
    public void PlayIdleAnimation()
    {
        if (idleAnimation != null)
        {
            Debug.Log("PlayAnimation(idleAnimation);");
            PlayAnimation(idleAnimation);
        }
    }
    
    public void PlayMoveAnimation()
    {
        if (moveAnimation != null)
        {
            PlayAnimation(moveAnimation);
        }
    }
    
    public void PlayDeadAnimation()
    {
        if (deadAnimation != null)
        {
            PlayAnimation(deadAnimation);
        }
    }
    
    private void PlayAnimation(AnimationClip clip)
    {
        if (enemyAnimator != null && clip != null)
        {
            enemyAnimator.Play(clip.name);
            currentAnimationState = clip.name;
        }
    }
    
    /// <summary>
    /// 播放appear动画序列
    /// </summary>
    private System.Collections.IEnumerator PlayAppearAnimationSequence()
    {
        // 播放appear动画
        PlayAppearAnimation();
        hasPlayedAppearAnimation = true;
        
        // 等待appear动画播放完成
        yield return new WaitForSeconds(appearAnimationDuration);
        
        // appear动画播放完成后，通知状态机可以开始正常工作
        if (stateMachine != null)
        {
            stateMachine.OnAppearAnimationCompleted();
        }
    }

    /// <summary>
    /// 身体部位闪烁效果
    /// </summary>
    /// <param name="bodyPart">身体部位</param>
    /// <returns></returns>
    private System.Collections.IEnumerator FlashBodyPart(BodyPart bodyPart)
    {
        if (bodyPart.partObject == null) yield break;
        
        SpriteRenderer spriteRenderer = bodyPart.partObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) yield break;
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = hitFlashColor;
        
        yield return new WaitForSeconds(hitFlashDuration);
        
        spriteRenderer.color = originalColor;
    }
    
    /// <summary>
    /// 显示血液效果
    /// </summary>
    /// <param name="position">位置</param>
    private void ShowBloodEffect(Vector3 position)
    {
        if (bloodEffectPrefab != null)
        {
            GameObject bloodEffect = Instantiate(bloodEffectPrefab, position, Quaternion.identity);
            Destroy(bloodEffect, 1f);
        }
    }
    
    /// <summary>
    /// 处理敌人朝向玩家的旋转逻辑
    /// </summary>
    private void HandleRotationTowardsPlayer()
    {
        if (target == null) return;
        
        // 计算从敌人到玩家的方向向量
        Vector3 directionToPlayer = (target.transform.position - transform.position).normalized;
        
        // 计算目标旋转角度
        // 在2D平面上，我们需要计算从正方向到目标方向的角度
        float targetAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;
        
        // 计算正方向的角度偏移
        float forwardAngle = Mathf.Atan2(forwardDirection.y, forwardDirection.x) * Mathf.Rad2Deg;
        
        // 计算最终的旋转角度（目标角度 - 正方向角度偏移）
        float finalAngle = targetAngle - forwardAngle;
        
        // 创建目标旋转（只在Z轴旋转）
        Quaternion targetRotation = Quaternion.Euler(0, 0, finalAngle);
        
        if (instantRotation)
        {
            // 瞬间旋转
            transform.rotation = targetRotation;
        }
        else
        {
            // 平滑旋转
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    // 公共访问器
    public float GetHealthPercentage() => currentHealth / maxHealth;
    public bool IsAnimationPlaying() => isPlayingAnimation;
    public string GetCurrentAnimationState() => currentAnimationState;
    public EnemyStateMachine GetStateMachine() => stateMachine;
    
    // 屏幕进入相关公共方法
    public void ForceEnterScreen()
    {
        if (!hasEnteredScreen)
        {
            CheckAndMoveToScreen();
        }
    }
    
    // 旋转设置相关公共方法
    /// <summary>
    /// 设置是否始终朝向玩家
    /// </summary>
    public void SetAlwaysFacePlayer(bool enabled)
    {
        alwaysFacePlayer = enabled;
    }
    
    /// <summary>
    /// 设置敌人的正方向
    /// </summary>
    public void SetForwardDirection(Vector3 direction)
    {
        forwardDirection = direction.normalized;
    }
    
    /// <summary>
    /// 设置旋转速度
    /// </summary>
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = Mathf.Max(0.1f, speed);
    }
    
    /// <summary>
    /// 设置是否瞬间旋转
    /// </summary>
    public void SetInstantRotation(bool instant)
    {
        instantRotation = instant;
    }
    
}

// 移动模式枚举
public enum MovementPattern
{
    Random,
    Path
}
