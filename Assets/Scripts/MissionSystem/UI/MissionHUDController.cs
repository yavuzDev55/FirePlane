using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MissionHUDController : MonoBehaviour
{
    [Header("References")]
    public UIDocument hudDocument;
    public MissionManager missionManager;

    // Runtime'da oluşturulan grup kartları — güncelleme için tutulur
    private struct ObjectiveUI
    {
        public MissionObjective objective;
        public Label descriptionLabel;
        public Label progressLabel;
        public Label iconLabel;
    }

    private List<ObjectiveUI> objectiveUIs = new();

    VisualElement missionHUD;

    void Start()
    {
        missionHUD = hudDocument.rootVisualElement.Q<VisualElement>("MissionHUD");

        // MissionManager grupları oluşturduktan sonra HUD'ı kur
        missionManager.OnMissionsActivated += BuildHUD;
    }

    void BuildHUD()
    {
        missionManager.OnMissionsActivated -= BuildHUD;

        // Mevcut içeriği temizle
        missionHUD.Clear();

        foreach (var group in missionManager.GetRuntimeGroups())
            BuildGroupCard(group);
    }

    void BuildGroupCard(MissionGroup group)
    {
        // Grup kartı
        VisualElement groupCard = new VisualElement();
        groupCard.AddToClassList("group-card");

        // Grup adı
        Label groupName = new Label(group.groupId);
        groupName.AddToClassList("group-name");
        groupCard.Add(groupName);

        // Ayraç
        VisualElement divider = new VisualElement();
        divider.AddToClassList("divider");
        groupCard.Add(divider);

        // Objective listesi
        VisualElement objectiveList = new VisualElement();
        objectiveList.AddToClassList("objective-list");
        groupCard.Add(objectiveList);

        // Grup içindeki node'ları işle
        BuildNodes(group.nodes, objectiveList);

        missionHUD.Add(groupCard);
    }

    // Özyinelemeli — iç içe grupları da işler
    void BuildNodes(List<IMissionNode> nodes, VisualElement parent)
    {
        foreach (var node in nodes)
        {
            if (node is MissionObjective objective)
            {
                BuildObjectiveRow(objective, parent);
            }
            else if (node is MissionGroup innerGroup)
            {
                // İç grup için girinti ekle
                VisualElement innerContainer = new VisualElement();
                innerContainer.AddToClassList("inner-group");
                parent.Add(innerContainer);

                // İç grup adı
                Label innerGroupName = new Label(innerGroup.groupId);
                innerGroupName.AddToClassList("group-name");
                innerContainer.Add(innerGroupName);

                BuildNodes(innerGroup.nodes, innerContainer);
            }
        }
    }

    void BuildObjectiveRow(MissionObjective objective, VisualElement parent)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("objective-row");

        // İkon
        Label icon = new Label("○");
        icon.AddToClassList("objective-icon");
        row.Add(icon);

        // Bilgi
        VisualElement info = new VisualElement();
        info.AddToClassList("objective-info");
        row.Add(info);

        // Açıklama
        Label description = new Label(objective.missionData.description);
        description.AddToClassList("objective-description");
        info.Add(description);

        // İlerleme
        Label progress = new Label();
        progress.AddToClassList("objective-progress");
        info.Add(progress);

        parent.Add(row);

        // Güncelleme için kaydet
        objectiveUIs.Add(new ObjectiveUI
        {
            objective = objective,
            descriptionLabel = description,
            progressLabel = progress,
            iconLabel = icon
        });
    }

    void Update()
    {
        UpdateObjectiveUIs();
    }

    void UpdateObjectiveUIs()
    {
        foreach (var ui in objectiveUIs)
        {
            if (ui.objective.IsFailed)
            {
                // Başarısız
                ui.iconLabel.text = "✕";
                ui.iconLabel.RemoveFromClassList("completed");
                ui.iconLabel.AddToClassList("failed");
                ui.descriptionLabel.RemoveFromClassList("completed");
                ui.descriptionLabel.AddToClassList("failed");
                ui.progressLabel.text = "Başarısız";
                ui.progressLabel.AddToClassList("failed");
            }
            else if (ui.objective.IsCompleted)
            {
                // Tamamlandı
                ui.iconLabel.text = "✓";
                ui.iconLabel.AddToClassList("completed");
                ui.descriptionLabel.AddToClassList("completed");
                ui.progressLabel.text = "";
            }
            else
            {
                // Devam ediyor
                ui.iconLabel.text = "○";
                float progress = ui.objective.GetProgress();
                ui.progressLabel.text = FormatProgress(ui.objective, progress);
            }
        }
    }

    // Görev tipine göre ilerlemeyi formatla
    string FormatProgress(MissionObjective objective, float progress)
    {
        if (objective is ExtinguishZoneMission)
        {
            // Yüzde göster — % 75 gibi
            return $"% {progress * 100:F0}";
        }
        else if (objective is ContainmentZoneMission)
        {
            // Tutuluyor / tutulmadı
            return progress >= 1f ? "Tutuluyor" : "İhlal edildi";
        }

        return "";
    }
}