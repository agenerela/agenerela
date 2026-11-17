using LivingVale.Combat;
using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Unity
{
    /// <summary>Shows the parcel's ending in the world and reports the camp's defeated defenders.</summary>
    public sealed class QuestWorldView : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private GameObject[] porchPatients;
        [SerializeField] private GameObject[] gateBars;
        [SerializeField] private GameObject[] divisionCrates;
        [SerializeField] private GameObject[] defenders;

        private EnemyController[] enemies;

        public void Configure(ValeBootstrap owner, GameObject[] porchPatients, GameObject[] gateBars,
            GameObject[] divisionCrates, GameObject[] defenders)
        {
            bootstrap = owner;
            this.porchPatients = porchPatients;
            this.gateBars = gateBars;
            this.divisionCrates = divisionCrates;
            this.defenders = defenders;
        }

        private void Start()
        {
            enemies = new EnemyController[defenders.Length];
            for (int i = 0; i < defenders.Length; i++)
            {
                if (defenders[i] != null)
                {
                    enemies[i] = defenders[i].GetComponent<EnemyController>();
                    if (enemies[i] != null)
                    {
                        enemies[i].Defeated += OnDefeated;
                    }
                }
            }

            bootstrap.Quests.Changed += Render;
            bootstrap.Routes.Changed += Render;
            Render();
        }

        private void OnDefeated(EnemyController enemy)
        {
            bootstrap.Routes.ReportDefeated(enemy.Body.Combatant.Id);
        }

        private void Render()
        {
            ParcelDisposition ending = bootstrap.Quests.MainState == MainQuestState.Resolved
                ? bootstrap.Quests.Disposition : ParcelDisposition.None;
            for (int i = 0; i < porchPatients.Length; i++)
            {
                if (porchPatients[i] != null)
                {
                    porchPatients[i].SetActive(ending != ParcelDisposition.Healer &&
                        !(ending == ParcelDisposition.Division && i == 0));
                }
            }

            SetActive(gateBars, ending != ParcelDisposition.Garrison);
            SetActive(divisionCrates, ending == ParcelDisposition.Division);
            if (bootstrap.Routes.CampCleared)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null && !enemy.Body.Combatant.IsDefeated)
                    {
                        // A load during combat must also stop the hidden enemy blocking conversation.
                        enemy.ResetToHome();
                        enemy.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // An older, uncleared save loaded after a cleared one: the defenders this view hid
                // come back, so the combat route can be fought again. Defeated ones stay down.
                foreach (var enemy in enemies)
                {
                    if (enemy != null && !enemy.gameObject.activeSelf && !enemy.Body.Combatant.IsDefeated)
                    {
                        enemy.gameObject.SetActive(true);
                    }
                }
            }
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            foreach (var item in objects)
            {
                if (item != null)
                {
                    item.SetActive(active);
                }
            }
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.Routes != null)
            {
                bootstrap.Quests.Changed -= Render;
                bootstrap.Routes.Changed -= Render;
            }

            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null)
                    {
                        enemy.Defeated -= OnDefeated;
                    }
                }
            }
        }
    }
}
