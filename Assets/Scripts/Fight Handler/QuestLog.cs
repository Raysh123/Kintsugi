using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The side task list. Tasks are listed in the Inspector; the rest of the game shows and completes them with
/// QuestLog.Show("Id") / QuestLog.Complete("Id") (safe to call when no QuestLog exists).
///
/// A task can also be driven from here, with no other script involved:
///  - Show When Completed: appears (after a short delay) when another task is completed.
///  - Show On Start: appears when the scene starts, if it hasn't been shown before.
///  - Complete On Wake: completed when the player wakes up from sleeping (and the task is showing).
///  - Progress Activity Ids: a counter task, e.g. "Finish training (0/3)". It counts how many of these
///    activities have been done today and completes at the full count. Put {0} and {1} in the text.
///  - Complete On Defeat Opponent Id: completed when the player wins a fight against that opponent.
///
/// Completed tasks get a line through them and then fade away, unless Keep When Done is ticked (they then stay
/// struck out). State is stored in PersistentGameFlags, so it survives scene reloads.
///
/// UI: a vertical layout object (Vertical Layout Group) on the side of the screen, plus a Row Prefab - a TMP
/// text with Rich Text on. Entries wait until no dialogue is open before appearing.
/// </summary>
public class QuestLog : MonoBehaviour
{
    [Serializable]
    public class QuestTask
    {
        public string id;
        [Tooltip("Shown in the list. For a counter task, put {0} (done) and {1} (total), e.g. Finish training ({0}/{1}).")]
        public string text;
        [Tooltip("Stay on the list struck out once done, instead of fading away.")]
        public bool keepWhenDone;
        public bool showOnStart;
        [Tooltip("Appears when the task with this id is completed.")]
        public string showWhenCompleted;
        [Tooltip("Completed when the player wakes up from sleeping, while this task is showing.")]
        public bool completeOnWake;
        [Tooltip("Counter task: the activity ids that count (each counted once per day). Leave empty for a normal task.")]
        public List<string> progressActivityIds = new List<string>();
        [Tooltip("Completed when the player wins a fight against this opponent id. Leave empty for none.")]
        public string completeOnDefeatOpponentId;
    }

    private class Row
    {
        public QuestTask task;
        public TMP_Text label;
        public CanvasGroup group;
        public bool done;
        public int progress;
    }

    public static QuestLog instance;

    [Header("UI")]
    [Tooltip("The object with a Vertical Layout Group that the rows are added to.")]
    [SerializeField] private RectTransform listRoot;
    [Tooltip("One row: a TMP text with Rich Text on. A CanvasGroup is added automatically.")]
    [SerializeField] private TMP_Text rowPrefab;

    [Header("Timing")]
    [SerializeField] private float popInTime = 0.25f;
    [Tooltip("How long a finished task stays struck out before it fades away.")]
    [SerializeField] private float doneLingerTime = 1.5f;
    [SerializeField] private float fadeTime = 0.4f;
    [Tooltip("Pause before the next task appears, so a finished task is seen being crossed out first.")]
    [SerializeField] private float nextShowDelay = 0.8f;

    [Header("Tasks")]
    [SerializeField] private List<QuestTask> tasks = new List<QuestTask>();

    public event Action<string> OnTaskCompleted;

    private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
    private readonly List<string> pendingDisplay = new List<string>();
    private bool displayRunning;
    private bool ready;
    private bool subscribedToCalendar;

    private static string ShownFlag(string id) => "Quest_" + id + "_Shown";
    private static string DoneFlag(string id) => "Quest_" + id + "_Done";
    private static bool HasFlag(string id) => PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(id);
    private static void SetFlag(string id) { if (PersistentGameFlags.instance != null) PersistentGameFlags.instance.SetFlag(id); }

    // ── Static API ───────────────────────────────────────────────────────────

    public static void Show(string id)
    {
        if (instance != null) instance.ShowTask(id);
    }

    public static void Complete(string id)
    {
        if (instance != null) instance.CompleteTask(id);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        instance = this;

        // A row template that lives in the scene is hidden so it doesn't show up as a stray entry itself.
        if (rowPrefab != null && rowPrefab.gameObject.scene.IsValid())
            rowPrefab.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        BedInteraction.OnAnyWakeUp += HandleWake;
    }

    private void OnDisable()
    {
        BedInteraction.OnAnyWakeUp -= HandleWake;
        if (subscribedToCalendar && CalendarManager.instance != null)
            CalendarManager.instance.OnTimeAdvanced -= HandleTimeAdvanced;
        subscribedToCalendar = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start()
    {
        StartCoroutine(Initialise());
    }

    private IEnumerator Initialise()
    {
        while (PersistentGameFlags.instance == null || CalendarManager.instance == null) yield return null;

        CalendarManager.instance.OnTimeAdvanced += HandleTimeAdvanced;
        subscribedToCalendar = true;

        // Put back whatever was on the list before a scene reload.
        foreach (QuestTask task in tasks)
        {
            if (string.IsNullOrEmpty(task.id) || !HasFlag(ShownFlag(task.id))) continue;

            bool done = HasFlag(DoneFlag(task.id));
            if (!done || task.keepWhenDone) AddRow(task, done, false);
        }

        ready = true;

        foreach (QuestTask task in tasks)
            if (task.showOnStart) ShowTask(task.id);

        if (pendingDisplay.Count > 0 && !displayRunning) StartCoroutine(DisplayPending());

        CheckDefeat();
    }

    // ── Showing and completing ───────────────────────────────────────────────

    private QuestTask Find(string id)
    {
        foreach (QuestTask task in tasks)
            if (task.id == id) return task;
        return null;
    }

    private void ShowTask(string id)
    {
        QuestTask task = Find(id);
        if (task == null || PersistentGameFlags.instance == null) return;
        if (HasFlag(ShownFlag(id))) return;

        SetFlag(ShownFlag(id));
        pendingDisplay.Add(id);
        if (ready && !displayRunning) StartCoroutine(DisplayPending());
    }

    private IEnumerator DisplayPending()
    {
        displayRunning = true;

        while (pendingDisplay.Count > 0)
        {
            while (ActiveDialogueTracker.AnyActive) yield return null;

            string id = pendingDisplay[0];
            pendingDisplay.RemoveAt(0);

            QuestTask task = Find(id);
            if (task != null && !rows.ContainsKey(id))
            {
                AddRow(task, HasFlag(DoneFlag(id)), true);
                UpdateProgress();
            }

            yield return new WaitForSecondsRealtime(nextShowDelay);
        }

        displayRunning = false;
    }

    private void CompleteTask(string id)
    {
        QuestTask task = Find(id);
        if (task == null || PersistentGameFlags.instance == null) return;
        if (!HasFlag(ShownFlag(id)) || HasFlag(DoneFlag(id))) return;

        SetFlag(DoneFlag(id));

        if (rows.TryGetValue(id, out Row row))
        {
            row.done = true;
            if (task.progressActivityIds.Count > 0) row.progress = task.progressActivityIds.Count;
            Refresh(row);
            if (!task.keepWhenDone) StartCoroutine(RemoveRow(row));
        }

        OnTaskCompleted?.Invoke(id);

        foreach (QuestTask next in tasks)
            if (next.showWhenCompleted == id) ShowTask(next.id);
    }

    // ── Automatic triggers ───────────────────────────────────────────────────

    private void HandleWake()
    {
        foreach (QuestTask task in tasks)
            if (task.completeOnWake) CompleteTask(task.id);
    }

    private void HandleTimeAdvanced(int tick)
    {
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (CalendarManager.instance == null) return;

        foreach (QuestTask task in tasks)
        {
            if (task.progressActivityIds.Count == 0) continue;
            if (!HasFlag(ShownFlag(task.id)) || HasFlag(DoneFlag(task.id))) continue;

            int count = 0;
            foreach (string activityId in task.progressActivityIds)
                if (CalendarManager.instance.HasDoneToday(activityId)) count++;

            if (rows.TryGetValue(task.id, out Row row) && row.progress != count)
            {
                row.progress = count;
                Refresh(row);
            }

            if (count >= task.progressActivityIds.Count) CompleteTask(task.id);
        }
    }

    private void CheckDefeat()
    {
        if (!FightSessionManager.wonLastFight || string.IsNullOrEmpty(FightSessionManager.opponentId)) return;

        foreach (QuestTask task in tasks)
            if (task.completeOnDefeatOpponentId == FightSessionManager.opponentId) CompleteTask(task.id);
    }

    // ── Rows ─────────────────────────────────────────────────────────────────

    private void AddRow(QuestTask task, bool done, bool animate)
    {
        if (rowPrefab == null || listRoot == null || rows.ContainsKey(task.id)) return;

        TMP_Text label = Instantiate(rowPrefab, listRoot);
        label.gameObject.SetActive(true);
        CanvasGroup group = label.GetComponent<CanvasGroup>();
        if (group == null) group = label.gameObject.AddComponent<CanvasGroup>();

        Row row = new Row { task = task, label = label, group = group, done = done };
        if (done && task.progressActivityIds.Count > 0) row.progress = task.progressActivityIds.Count;
        rows[task.id] = row;
        Refresh(row);

        if (animate) StartCoroutine(PopIn(row));
    }

    private void Refresh(Row row)
    {
        string text = row.task.text;
        if (row.task.progressActivityIds.Count > 0)
            text = string.Format(text, row.progress, row.task.progressActivityIds.Count);

        row.label.text = row.done ? "<s>" + text + "</s>" : text;
        row.group.alpha = row.done ? 0.6f : 1f;
    }

    private IEnumerator PopIn(Row row)
    {
        Transform t = row.label.transform;
        float elapsed = 0f;

        while (elapsed < popInTime && row.label != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / popInTime);
            row.group.alpha = k;
            t.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k);
            yield return null;
        }

        if (row.label == null) yield break;
        t.localScale = Vector3.one;
        row.group.alpha = row.done ? 0.6f : 1f;
    }

    private IEnumerator RemoveRow(Row row)
    {
        yield return new WaitForSecondsRealtime(doneLingerTime);

        float elapsed = 0f;
        float startAlpha = row.group.alpha;
        while (elapsed < fadeTime && row.label != null)
        {
            elapsed += Time.unscaledDeltaTime;
            row.group.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeTime);
            yield return null;
        }

        if (row.label == null) yield break;
        rows.Remove(row.task.id);
        Destroy(row.label.gameObject);
    }
}