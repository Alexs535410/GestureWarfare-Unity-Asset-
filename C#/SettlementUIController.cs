using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic; // Added for List


public class SettlementUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject title;
    [SerializeField] private GameObject scoreRecord;
    [SerializeField] private GameObject scorePoint;
    [SerializeField] private GameObject startButtonPrefab; // Enemy_0_Button预制体
    [SerializeField] private GameObject reStartButton; // 重新开始按钮（作为SettlementUI的子物体）
    
    [Header("Settlement Data")]
    [SerializeField] private string levelTitle = "Level Completed!";
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int enemiesKilled = 0;
    [SerializeField] private int bossesKilled = 0;
    
    [Header("Background Camera Control")]
    [SerializeField] private BackgroundCameraController backgroundCameraController;
    [SerializeField] private string settlementSceneName = "Settlement"; // 结算场景名称

    private string RecordStr = ""; // scoreRecord将要打印的内容
    private string PointStr = "";  // scorePoint将要打印的内容

    private GameManager gameManager;
    private StartButton currentStartButton;
    private Button reStartButtonComponent; // ReStart按钮组件

    private bool startShowText = false; // 开始展示text内容 这个变量需要在动画中设置
    private float typeInterval = 0.01f;
    
    private void Start()
    {
        gameManager = GameManager.Instance;
        
        // 如果没有手动指定背景相机控制器，尝试自动查找
        if (backgroundCameraController == null)
        {
            backgroundCameraController = FindObjectOfType<BackgroundCameraController>();
        }
        
        // 初始化ReStart按钮
        InitializeReStartButton();
        
        SetupSettlementUI();
        
        // 切换到结算场景
        SwitchToSettlementScene();
    }
    
    /// <summary>
    /// 初始化ReStart按钮
    /// </summary>
    private void InitializeReStartButton()
    {
        if (reStartButton != null)
        {
            reStartButtonComponent = reStartButton.GetComponent<Button>();
            if (reStartButtonComponent != null)
            {
                reStartButtonComponent.onClick.AddListener(OnReStartButtonClicked);
            }
            
            // 默认隐藏ReStart按钮
            reStartButton.SetActive(false);
        }
    }
    
    /// <summary>
    /// 显示ReStart按钮（当所有关卡完成时调用）
    /// </summary>
    public void ShowReStartButton()
    {
        if (reStartButton != null)
        {
            reStartButton.SetActive(true);
            Debug.Log("ReStart button activated for game completion");
        }
    }
    
    /// <summary>
    /// 隐藏ReStart按钮
    /// </summary>
    public void HideReStartButton()
    {
        if (reStartButton != null)
        {
            reStartButton.SetActive(false);
        }
    }
    
    /// <summary>
    /// ReStart按钮点击事件
    /// </summary>
    private void OnReStartButtonClicked()
    {
        Debug.Log("ReStart button clicked - restarting from beginning");
        
        if (gameManager != null)
        {
            // 隐藏结算界面
            gameObject.SetActive(false);
            
            // 调用GameManager的重头再来方法
            gameManager.RestartFromBeginning();
        }
    }
    
    /// <summary>
    /// 设置结算界面
    /// </summary>
    private void SetupSettlementUI()
    {
        // 更新标题
        if (title != null)
        {
            TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = levelTitle;
            }
        }
        
        // 更新得分记录
        //UpdateScoreDisplay();
        
        // 生成开始按钮
        CreateStartButton();
    }

    /// <summary>
    /// 创建开始按钮
    /// </summary>
    private void CreateStartButton()
    {
        // 启动协程，3秒后创建按钮
        StartCoroutine(CreateStartButtonAfterDelay());
    }

    /// <summary>
    /// 延迟创建开始按钮的协程
    /// </summary>
    private System.Collections.IEnumerator CreateStartButtonAfterDelay()
    {
        // 等待3秒
        yield return new WaitForSeconds(3f);
        
        if (startButtonPrefab != null)
        {
            GameObject buttonObj = Instantiate(startButtonPrefab, transform);
            currentStartButton = buttonObj.GetComponent<StartButton>();

            if (currentStartButton != null)
            {
                // 订阅按钮事件
                currentStartButton.OnButtonActivated += OnStartButtonActivated;
            }
            
            Debug.Log("Start button created after 3 seconds");
        }
    }

    /// <summary>
    /// 切换到结算场景
    /// </summary>
    private void SwitchToSettlementScene()
    {
        if (backgroundCameraController != null)
        {
            backgroundCameraController.SwitchToScene(settlementSceneName, true);
            Debug.Log($"切换到结算场景: {settlementSceneName}");
        }
        else
        {
            Debug.LogWarning("SettlementUIController: 未找到BackgroundCameraController");
        }
    }
    
    /// <summary>
    /// 开始按钮激活回调
    /// </summary>
    private void OnStartButtonActivated(StartButton button)
    {
        // 在开始下一关之前，可以切换到游戏场景
        if (backgroundCameraController != null)
        {
            backgroundCameraController.SwitchToScene("Game", true);
        }
        
        if (gameManager != null)
        {
            gameManager.StartNextLevelFromSettlement();
        }
    }
    
    /// <summary>
    /// 手动切换背景场景（供外部调用）
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    public void SwitchBackgroundScene(string sceneName)
    {
        if (backgroundCameraController != null)
        {
            backgroundCameraController.SwitchToScene(sceneName, true);
        }
    }
    
    /// <summary>
    /// 手动切换背景场景（通过索引）
    /// </summary>
    /// <param name="sceneIndex">场景索引</param>
    public void SwitchBackgroundScene(int sceneIndex)
    {
        if (backgroundCameraController != null)
        {
            backgroundCameraController.SwitchToScene(sceneIndex, true);
        }
    }

    /// <summary>
    /// 更新得分显示
    /// </summary>
    private void UpdateScoreDisplay()
    {
        if (scoreRecord != null)
        {
            TextMeshProUGUI recordText = scoreRecord.GetComponent<TextMeshProUGUI>();
            if (recordText != null)
            {
                recordText.text = "";
            }
        }
        
        if (scorePoint != null)
        {
            TextMeshProUGUI pointText = scorePoint.GetComponent<TextMeshProUGUI>();
            if (pointText != null)
            {
                pointText.text = "";
            }
        }
        
        // 准备要显示的内容
        PrepareSettlementContent();
        
        // 开始打字机效果
        if (startShowText)
        {
            StartCoroutine(ShowSettlementContent());
        }
    }
    
    /// <summary>
    /// 准备结算内容
    /// </summary>
    private void PrepareSettlementContent()
    {
        // 从GameManager获取击杀记录
        if (gameManager != null)
        {
            List<EnemySpawner.KillRecord> killRecords = gameManager.GetCurrentLevelKillRecords();
            int totalScore = gameManager.GetCurrentLevelTotalScore();
            
            // 构建RecordStr和PointStr
            RecordStr = "";
            PointStr = "";
            
            foreach (var record in killRecords)
            {
                RecordStr += $"{record.enemyName} * {record.killCount}\n";
                PointStr += $"{record.GetTotalScore()}\n";
            }
            
            RecordStr += $"\nTotal";
            PointStr +=  $"\n" + totalScore.ToString();

            //Debug.Log(RecordStr);
            //Debug.Log(PointStr);
        }
    }
    
    /// <summary>
    /// 显示结算内容的打字机效果
    /// </summary>
    private System.Collections.IEnumerator ShowSettlementContent()
    {
        // 先显示Record内容
        yield return StartCoroutine(TypeRecord());
        
        // 等待一小段时间
        yield return new WaitForSeconds(0.5f);
        
        // 再显示Point内容
        yield return StartCoroutine(TypePoint());
    }
    
    /// <summary>
    /// 设置结算数据
    /// </summary>
    public void SetSettlementData(string title, List<EnemySpawner.KillRecord> killRecords, int totalScore)
    {
        levelTitle = title;
        
        // 构建显示内容
        RecordStr = "";
        PointStr = "";
        
        foreach (var record in killRecords)
        {
            RecordStr += $"{record.enemyName} * {record.killCount}\n";
            PointStr += $"{record.GetTotalScore()}\n";
        }

        RecordStr += $"\nTotal";
        PointStr += $"\n" + totalScore.ToString();

        // 开始显示
        startShowText = true;
        UpdateScoreDisplay();
    }
    
    /// <summary>
    /// 设置开始显示文本（由动画调用）
    /// </summary>
    public void SetStartShowText(bool start)
    {
        startShowText = start;
        if (start)
        {
            UpdateScoreDisplay();
        }
    }
    
    private System.Collections.IEnumerator TypeRecord()
    {
        TextMeshProUGUI recordText = scoreRecord.GetComponent<TextMeshProUGUI>();
        if (recordText != null)
        {
            recordText.text = "";
            foreach (char c in RecordStr)
            {
                recordText.text += c;
                yield return new WaitForSeconds(typeInterval);
            }
        }
    }

    private System.Collections.IEnumerator TypePoint()
    {
        TextMeshProUGUI pointText = scorePoint.GetComponent<TextMeshProUGUI>();
        if (pointText != null)
        {
            pointText.text = "";
            foreach (char c in PointStr)
            {
                pointText.text += c;
                yield return new WaitForSeconds(typeInterval);
            }
        }
    }
    
    private void OnDestroy()
    {
        // 清理事件订阅
        if (reStartButtonComponent != null)
        {
            reStartButtonComponent.onClick.RemoveListener(OnReStartButtonClicked);
        }
        
        if (currentStartButton != null)
        {
            currentStartButton.OnButtonActivated -= OnStartButtonActivated;
        }
    }
}
