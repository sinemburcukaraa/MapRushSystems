using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public abstract class JokerBase 
{
    public JokerType type;
    public Sprite icon;
    public int Count = 1; 

        public JokerBase( JokerType type, Sprite icon,int Count)
        {
            this.type = type;
            this.icon = icon;
            this.Count = Count;
        }
    
        public bool HasAvailable() => Count > 0;
        public abstract void Use(Vector2Int selectedTile, Vector2Int targetTile = default);
        public abstract void HandleClick(PathController tile);

    }

    public class SwitchJoker : JokerBase
    {
        public Vector2Int? FirstSelected; 
        public SwitchJoker() : base(JokerType.Switch, Resources.Load<Sprite>("Icons/Switch"), 1) { }

        public override void Use(Vector2Int selectedTile, Vector2Int targetTile = default)
        {
            if (targetTile == default) return;

            // Prevent switching start/end tiles
            if (JokerManager.Instance != null && (JokerManager.Instance.IsStartOrEnd(selectedTile) || JokerManager.Instance.IsStartOrEnd(targetTile)))
            {
                Debug.Log("Cannot use Switch Joker on start or end tile.");
                return;
            }

            var firstTile = PathManager.instance.coordToTile[selectedTile];
            var secondTile = PathManager.instance.coordToTile[targetTile];

            // Basit animasyonlu swap örneği
            Vector3 tempPos = firstTile.transform.position;
            firstTile.transform.position = secondTile.transform.position;
            secondTile.transform.position = tempPos;

            Debug.Log($"Switched tiles: {selectedTile} <-> {targetTile}");
        }

        public override void HandleClick(PathController tile)
        {
            if (!FirstSelected.HasValue)
            {
                // Do not allow selecting start/end as first
                if (JokerManager.Instance != null && JokerManager.Instance.IsStartOrEnd(tile.Coord))
                {
                    Debug.Log("Cannot select start/end tile for Switch Joker.");
                    return;
                }

                FirstSelected = tile.Coord;
                Debug.Log("First tile selected for switch: " + tile.Coord);
            }
            else
            {
                Vector2Int first = FirstSelected.Value;
                Vector2Int second = tile.Coord;

                // Prevent using on start/end
                if (JokerManager.Instance != null && (JokerManager.Instance.IsStartOrEnd(first) || JokerManager.Instance.IsStartOrEnd(second)))
                {
                    Debug.Log("Cannot use Switch Joker on start or end tile.");
                    FirstSelected = null;
                    JokerManager.Instance.DeselectJoker();
                    return;
                }

                // Clear selection and delegate actual use & consumption to JokerManager
                FirstSelected = null;
                if (JokerManager.Instance != null)
                    JokerManager.Instance.UseJoker(first, second);
            }
        }

    
    }

    public class ShowPathJoker : JokerBase
    {
        public float revealDuration = 5f;

        public ShowPathJoker(float duration = 5f) : base(JokerType.ShowPath, Resources.Load<Sprite>("Icons/ShowPath"), 1)
        {
            revealDuration = duration;
        }

        public override void Use(Vector2Int selectedTile, Vector2Int targetTile = default)
        {
            var pm = PathManager.instance;
            var mm = MapManager.instance;
            if (pm == null || mm == null)
            {
                Debug.Log("PathManager or MapManager not available.");
                return;
            }

            List<Vector2Int> path = null;
            // Prefer a validated finalPath if available
            if (pm.IsValidPath(mm.startCoord, mm.endCoord) && pm.finalPath != null && pm.finalPath.Count > 0)
                path = new List<Vector2Int>(pm.finalPath);
            else
                path = pm.GetTraversablePathFromStart(mm.startCoord, mm.endCoord);

            if (path == null || path.Count == 0)
            {
                Debug.Log("No path found to reveal.");
                return;
            }

            // Highlight path tiles green
            foreach (var coord in path)
            {
                if (pm.coordToTile.TryGetValue(coord, out var tile))
                {
                    tile.SetHighlight(Color.green);
                }
            }

            // Start coroutine to clear highlights after duration
            if (JokerManager.Instance != null)
                JokerManager.Instance.StartCoroutine(RevealCoroutine(path));

            // Consumption is handled by JokerManager.UseJoker when selected/activated
        }

        public override void HandleClick(PathController tile)
        {
            if (JokerManager.Instance != null)
                JokerManager.Instance.UseJoker(default, default);
        }

        private IEnumerator RevealCoroutine(List<Vector2Int> path)
        {
            yield return new WaitForSecondsRealtime(revealDuration);

            var pm = PathManager.instance;
            if (pm == null) yield break;

            foreach (var coord in path)
            {
                if (pm.coordToTile.TryGetValue(coord, out var tile))
                {
                    tile.ClearHighlight();
                }
            }
        }
    }

    public class ExtraMoveJoker : JokerBase
    {
        public int extraMoves = 1;

        public ExtraMoveJoker(int extra = 1) : base(JokerType.ExtraMove, Resources.Load<Sprite>("Icons/ExtraMove"), 1)
        {
            extraMoves = extra;
        }

        public override void Use(Vector2Int selectedTile, Vector2Int targetTile = default)
        {
            var gm = GameManager.instance;
            if (gm == null)
            {
                Debug.Log("GameManager not found for ExtraMoveJoker.");
                return;
            }

            gm.AddMoves(extraMoves);
            Debug.Log($"Granted {extraMoves} extra moves.");

            // Consumption handled centrally by JokerManager
        }

        public override void HandleClick(PathController tile)
        {
            if (JokerManager.Instance != null)
                JokerManager.Instance.UseJoker(default, default);
        }
    }
    // public class ExtraMoveJoker : JokerBase
    // {
    //     public ExtraMoveJoker() : base(JokerType.ExtraMove, Resources.Load<Sprite>("Icons/ExtraMove"), 1)
    //     {
    //     }
    //
    //     public override void Use(Vector2Int selectedTile, Vector2Int targetTile = default)
    //     {
    //         // GameManager.Instance.AddExtraMove(1);
    //         Debug.Log("Extra Move Joker Activated");
    //     }
    //
    //     public void ActivateExtraMove()
    //     {
    //         // Logic to grant an extra move to the player
    //         Debug.Log("Extra Move Granted to Player");
    //     }
    // }
    public class ReplaceTileJoker : JokerBase
    {
        public GameObject replacementPrefab;

        public ReplaceTileJoker(GameObject prefab) 
            : base(JokerType.ReplaceTile, Resources.Load<Sprite>("Icons/ReplaceTile"), 1)
        {
            replacementPrefab = prefab;
        }

        public override void Use(Vector2Int selectedTile, Vector2Int targetTile = default)
        {
            if (!MapManager.instance.GridTilePositions.ContainsKey(selectedTile))
                return;

            if (JokerManager.Instance != null && JokerManager.Instance.IsStartOrEnd(selectedTile))
            {
                Debug.Log("Cannot use ReplaceTile Joker on start or end tile.");
                return;
            }

            if (!PathManager.instance.coordToTile.TryGetValue(selectedTile, out var oldTile))
                return;

            Vector3 pos = oldTile.transform.position;
            Quaternion rot = oldTile.transform.rotation;
            Transform parent = oldTile.transform.parent;

            // Eski tile’ı silmeden animasyon
            Sequence seq = DOTween.Sequence();
            seq.Append(oldTile.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            seq.OnComplete(() =>
            {
                Object.Destroy(oldTile.gameObject);

                // Yeni prefab oluştur
                GameObject newTileGO = Object.Instantiate(replacementPrefab, pos, rot, parent);
                Debug.Log("newTileGO instantiated"+ newTileGO);
                if (newTileGO.TryGetComponent<PathController>(out var newTileCtrl))
                {
                    newTileCtrl.SetCoord(selectedTile);
                    PathManager.instance.coordToTile[selectedTile] = newTileCtrl;
                }

                newTileGO.transform.localScale = Vector3.zero;
                newTileGO.transform.DOScale(0.485f, 0.3f)
                    .SetEase(Ease.OutBack);

                Debug.Log("Tile replaced at: " + selectedTile);
            });

            // Consumption handled by JokerManager
        }

        public override void HandleClick(PathController tile)
        {
            if (JokerManager.Instance != null)
                JokerManager.Instance.UseJoker(tile.Coord, default);
        }
    }

    public class FreezeTimeJoker : JokerBase
    {
        public float freezeDuration = 7f;

        public FreezeTimeJoker(float duration = 7f) : base(JokerType.FreezeTime, Resources.Load<Sprite>("Icons/FreezeTime"), 1)
        {
            freezeDuration = duration;
        }

        public override void Use(Vector2Int selectedTile, Vector2Int targetTile = default)
        {
            // Prevent using on start/end if a tile was targeted
            if (JokerManager.Instance != null && selectedTile != default && JokerManager.Instance.IsStartOrEnd(selectedTile))
            {
                Debug.Log("Cannot use FreezeTime Joker on start or end tile.");
                return;
            }

            var jm = JokerManager.Instance;
            if (jm == null) return;

            var timer = GameManager.instance?.GetComponent<GameTimer>();
            if (timer == null)
            {
                Debug.Log("No GameTimer found to freeze.");
                return;
            }

            jm.StartCoroutine(FreezeCoroutine(timer));

            // Consumption handled by JokerManager
        }

        public override void HandleClick(PathController tile)
        {
            // If clicked on start/end, disallow
            if (JokerManager.Instance != null && JokerManager.Instance.IsStartOrEnd(tile.Coord))
            {
                Debug.Log("Cannot use FreezeTime Joker on start or end tile.");
                return;
            }

            if (JokerManager.Instance != null)
                JokerManager.Instance.UseJoker(tile.Coord, default);
        }

        private IEnumerator FreezeCoroutine(GameTimer timer)
        {
            Debug.Log($"Freezing time for {freezeDuration} seconds.");
            bool wasRunning = timer != null && timer.GetRemainingTime() > 0f;
            timer.PauseTimer();
            yield return new WaitForSecondsRealtime(freezeDuration);
            if (wasRunning)
                timer.ResumeTimer();
            Debug.Log("Time freeze ended.");
        }
    }