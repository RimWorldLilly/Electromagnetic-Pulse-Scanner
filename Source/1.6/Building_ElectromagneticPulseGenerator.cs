using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ElectromagneticPulseGenerator
{
    public class Building_ElectromagneticPulseGenerator : Building, IExposable
    {
        // For incremental scanning
        private int scanIndex = 0;
        private int scanCooldown = 0;
        private CompPowerTrader powerComp;
        private List<IntVec3> oreCells = new List<IntVec3>();
        private List<IntVec3> revealedOreCells = new List<IntVec3>();
        private List<IntVec3> unrevealedOreCells = new List<IntVec3>();
        private List<List<IntVec3>> oreDeposits = new List<List<IntVec3>>();
        private List<List<IntVec3>> revealedDeposits = new List<List<IntVec3>>();
        private List<List<IntVec3>> unrevealedDeposits = new List<List<IntVec3>>();
        private readonly List<List<IntVec3>> incrementalScanDeposits = new List<List<IntVec3>>();
        private readonly HashSet<IntVec3> incrementalScanVisited = new HashSet<IntVec3>();
        private List<IntVec3> savedRevealedDepositCells = new List<IntVec3>();
        private List<int> savedRevealedDepositSizes = new List<int>();
        private List<IntVec3> savedUnrevealedDepositCells = new List<IntVec3>();
        private List<int> savedUnrevealedDepositSizes = new List<int>();
        private bool scanned = false;
        private bool autoMineEnabled = false;

        // For save/load
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref scanned, "scanned", false);
            Scribe_Values.Look<bool>(ref autoMineEnabled, "autoMineEnabled", false);

            // Serialize nested deposit lists as flat cell arrays plus per-deposit lengths.
            // LookMode.Value is valid for IntVec3 and int, but not for List<IntVec3>.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                FlattenDeposits(revealedDeposits, ref savedRevealedDepositCells, ref savedRevealedDepositSizes);
                FlattenDeposits(unrevealedDeposits, ref savedUnrevealedDepositCells, ref savedUnrevealedDepositSizes);
            }

            Scribe_Collections.Look(ref savedRevealedDepositCells, "revealedDepositCells", LookMode.Value);
            Scribe_Collections.Look(ref savedRevealedDepositSizes, "revealedDepositSizes", LookMode.Value);
            Scribe_Collections.Look(ref savedUnrevealedDepositCells, "unrevealedDepositCells", LookMode.Value);
            Scribe_Collections.Look(ref savedUnrevealedDepositSizes, "unrevealedDepositSizes", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                revealedDeposits = RestoreDeposits(savedRevealedDepositCells, savedRevealedDepositSizes);
                unrevealedDeposits = RestoreDeposits(savedUnrevealedDepositCells, savedUnrevealedDepositSizes);
                if (revealedDeposits == null) revealedDeposits = new List<List<IntVec3>>();
                if (unrevealedDeposits == null) unrevealedDeposits = new List<List<IntVec3>>();
                if (oreDeposits == null) oreDeposits = new List<List<IntVec3>>();
                oreDeposits.Clear();
                oreDeposits.AddRange(revealedDeposits);
                oreDeposits.AddRange(unrevealedDeposits);
                incrementalScanDeposits.Clear();
                incrementalScanVisited.Clear();
                scanIndex = 0;
            }
        }

        private static void FlattenDeposits(
            List<List<IntVec3>> deposits,
            ref List<IntVec3> cells,
            ref List<int> sizes)
        {
            if (cells == null) cells = new List<IntVec3>();
            if (sizes == null) sizes = new List<int>();
            cells.Clear();
            sizes.Clear();

            if (deposits == null) return;
            foreach (List<IntVec3> deposit in deposits)
            {
                if (deposit == null || deposit.Count == 0) continue;
                sizes.Add(deposit.Count);
                cells.AddRange(deposit);
            }
        }

        private static List<List<IntVec3>> RestoreDeposits(
            List<IntVec3> cells,
            List<int> sizes)
        {
            List<List<IntVec3>> deposits = new List<List<IntVec3>>();
            if (cells == null || sizes == null) return deposits;

            int offset = 0;
            foreach (int size in sizes)
            {
                if (size <= 0 || offset + size > cells.Count)
                {
                    deposits.Clear();
                    return deposits;
                }

                List<IntVec3> deposit = new List<IntVec3>(size);
                for (int i = 0; i < size; i++)
                    deposit.Add(cells[offset + i]);
                deposits.Add(deposit);
                offset += size;
            }

            if (offset != cells.Count)
                deposits.Clear();

            return deposits;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();

            // Rebuild derived collections after loading without changing discovery state.
            if (revealedDeposits == null) revealedDeposits = new List<List<IntVec3>>();
            if (unrevealedDeposits == null) unrevealedDeposits = new List<List<IntVec3>>();
            if (oreDeposits == null) oreDeposits = new List<List<IntVec3>>();
            oreDeposits.Clear();
            oreDeposits.AddRange(revealedDeposits);
            oreDeposits.AddRange(unrevealedDeposits);
            incrementalScanDeposits.Clear();
            incrementalScanVisited.Clear();
            scanIndex = 0;
            if (scanned && scanCooldown <= 0) scanCooldown = 5000;
        }

        protected override void Tick()
        {
            base.Tick();
            if (!scanned && powerComp != null && powerComp.PowerOn)
            {
                ScanForOres(Map);
                scanned = true;
            }
            // Incremental rescan for new deposits
            if (powerComp != null && powerComp.PowerOn)
            {
                if (scanCooldown > 0)
                {
                    scanCooldown--;
                }
                else
                {
                    IncrementalScanForOres(Map);
                }
            }
        }

        // Incremental scan for new deposits (scans 100 things per tick)
        // Scan a bounded batch of things each tick, then merge the completed scan.
        // Existing revealed deposits remain revealed across subsequent scan cycles.
        private void IncrementalScanForOres(Map map)
        {
            var allThings = map.listerThings.AllThings;
            const int batchSize = 100;

            if (scanIndex == 0)
            {
                incrementalScanDeposits.Clear();
                incrementalScanVisited.Clear();
            }

            int end = Mathf.Min(scanIndex + batchSize, allThings.Count);
            for (int i = scanIndex; i < end; i++)
            {
                Thing thing = allThings[i];
                if (!(thing is Mineable) || !IsOre(thing.def))
                    continue;

                IntVec3 pos = thing.Position;
                if (incrementalScanVisited.Contains(pos))
                    continue;

                List<IntVec3> deposit = new List<IntVec3>();
                FloodFillOre(map, pos, thing.def, incrementalScanVisited, deposit);
                if (deposit.Count > 0)
                    incrementalScanDeposits.Add(deposit);
            }

            scanIndex = end;
            if (scanIndex >= allThings.Count)
            {
                MergeIncrementalScanResults(map);
                scanIndex = 0;
                scanCooldown = 5000;
                incrementalScanDeposits.Clear();
                incrementalScanVisited.Clear();
            }
        }

        private void MergeIncrementalScanResults(Map map)
        {
            HashSet<IntVec3> previouslyRevealed = new HashSet<IntVec3>(
                revealedDeposits.Where(deposit => deposit != null).SelectMany(deposit => deposit));
            HashSet<IntVec3> previouslyUnrevealed = new HashSet<IntVec3>(
                unrevealedDeposits.Where(deposit => deposit != null).SelectMany(deposit => deposit));

            List<List<IntVec3>> nextRevealed = new List<List<IntVec3>>();
            List<List<IntVec3>> nextUnrevealed = new List<List<IntVec3>>();
            oreDeposits.Clear();

            foreach (List<IntVec3> deposit in incrementalScanDeposits)
            {
                if (deposit == null || deposit.Count == 0)
                    continue;

                oreDeposits.Add(deposit);
                bool wasRevealed = deposit.Any(cell => previouslyRevealed.Contains(cell));
                bool wasKnownUnrevealed = deposit.Any(cell => previouslyUnrevealed.Contains(cell));

                if (wasRevealed)
                    nextRevealed.Add(deposit);
                else
                    nextUnrevealed.Add(deposit);
            }

            revealedDeposits = nextRevealed;
            unrevealedDeposits = nextUnrevealed;
            RemoveMinedDeposits(map);
        }

        private void ScanForOres(Map map)
        {
            oreCells.Clear();
            revealedOreCells.Clear();
            unrevealedOreCells.Clear();
            oreDeposits.Clear();
            revealedDeposits.Clear();
            unrevealedDeposits.Clear();
            var allThings = map.listerThings.AllThings;
            HashSet<IntVec3> visited = new HashSet<IntVec3>();
            foreach (var thing in allThings)
            {
                if (thing is Mineable && IsOre(thing.def))
                {
                    IntVec3 pos = thing.Position;
                    if (!visited.Contains(pos))
                    {
                        // Flood fill to find the whole deposit
                        List<IntVec3> deposit = new List<IntVec3>();
                        FloodFillOre(map, pos, thing.def, visited, deposit);
                        if (deposit.Count > 0)
                        {
                            oreDeposits.Add(deposit);
                            unrevealedDeposits.Add(deposit);
                        }
                    }
                }
            }
            scanCooldown = 5000;
        }

        private bool IsOre(ThingDef def)
        {
            // Most ores have building.isResourceRock true, but not MineableRock
            return def.building != null && def.building.isResourceRock;
        }

        // Flood fill to group connected ore cells of the same type
        private void FloodFillOre(Map map, IntVec3 start, ThingDef oreDef, HashSet<IntVec3> visited, List<IntVec3> deposit)
        {
            Queue<IntVec3> queue = new Queue<IntVec3>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                deposit.Add(cell);
                foreach (IntVec3 adj in GenAdj.AdjacentCellsAndInside)
                {
                    IntVec3 next = cell + adj;
                    if (next.InBounds(map) && !visited.Contains(next))
                    {
                        Thing thing = map.thingGrid.ThingAt(next, oreDef);
                        if (thing != null && thing.def == oreDef)
                        {
                            queue.Enqueue(next);
                            visited.Add(next);
                        }
                    }
                }
            }
        }
        // Reveal a random unrevealed deposit
        public bool RevealRandomOreDeposit(Map map)
        {
            // Remove any deposits that have been fully mined before revealing
            RemoveMinedDeposits(map);
            if (unrevealedDeposits.Count == 0) return false;
            int idx = Rand.Range(0, unrevealedDeposits.Count);
            var deposit = unrevealedDeposits[idx];
            unrevealedDeposits.RemoveAt(idx);
            revealedDeposits.Add(deposit);
            foreach (var cell in deposit)
            {
                if (map.fogGrid.IsFogged(cell))
                {
                    map.fogGrid.Unfog(cell);
                }
            }
            // Center camera on the first cell of the deposit
            IntVec3 centerCell = deposit[0];
            // Get ore type from the first cell
            Mineable oreThing = map.thingGrid.ThingAt<Mineable>(centerCell);
            string oreLabel = oreThing?.def?.label ?? "ore";
            Messages.Message($"A new {oreLabel} deposit has been revealed!", new TargetInfo(centerCell, map), MessageTypeDefOf.PositiveEvent);
            if (autoMineEnabled)
            {
                DesignateDepositAndMineShaft(map, deposit);
            }
            return true;
        }
        // Designate the deposit and a mine shaft for mining
        private void DesignateDepositAndMineShaft(Map map, List<IntVec3> deposit)
        {
            // Designate all deposit cells for mining
            foreach (var cell in deposit)
            {
                Mineable mineable = map.thingGrid.ThingAt<Mineable>(cell);
                if (mineable != null && mineable.def.building != null && mineable.def.building.isResourceRock)
                {
                    if (map.designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null)
                    {
                        map.designationManager.AddDesignation(new Designation(mineable, DesignationDefOf.Mine));
                    }
                }
            }
            // Find closest reachable unfogged cell
            IntVec3? start = FindClosestUnfoggedCell(map, deposit);
            if (start == null) return;
            // Find closest cell in deposit to start
            IntVec3 end = deposit.OrderBy(c => c.DistanceTo(start.Value)).First();
            // Designate a straight line (mine shaft), skipping cells already in the deposit
            foreach (var cell in GenSight.PointsOnLineOfSight(start.Value, end))
            {
                if (deposit.Contains(cell)) continue; // Skip already designated deposit cells
                Mineable mineable = map.thingGrid.ThingAt<Mineable>(cell);
                if (mineable != null)
                {
                    if (map.designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null)
                    {
                        map.designationManager.AddDesignation(new Designation(mineable, DesignationDefOf.Mine));
                    }
                }
            }
        }
        // Find the closest unfogged and standable cell to the deposit (prefer straight over diagonal if distance is equal)
        private IntVec3? FindClosestUnfoggedCell(Map map, List<IntVec3> deposit)
        {
            int minDist = int.MaxValue;
            IntVec3? best = null;
            bool bestIsStraight = false;
            foreach (var cell in map.AllCells)
            {
                if (!map.fogGrid.IsFogged(cell) && cell.Standable(map))
                {
                    int dist = Mathf.RoundToInt(deposit.Min(d => d.DistanceTo(cell)));
                    bool isStraight = deposit.Any(d => d.x == cell.x || d.z == cell.z);
                    if (dist < minDist || (dist == minDist && isStraight && !bestIsStraight))
                    {
                        minDist = dist;
                        best = cell;
                        bestIsStraight = isStraight;
                    }
                }
            }
            return best;
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (powerComp != null && powerComp.PowerOn && revealedDeposits.Count > 0)
            {
                // Only draw overlays for deposits that still have ore
                foreach (var deposit in revealedDeposits)
                {
                    if (deposit.Any(cell => Map.thingGrid.ThingAt<Mineable>(cell) != null))
                    {
                        GenDraw.DrawFieldEdges(deposit, Color.green);
                    }
                }
            }
        }
        // Remove deposits that have been fully mined from all lists
        private void RemoveMinedDeposits(Map map)
        {
            bool HasOre(List<IntVec3> deposit) => deposit.Any(cell => map.thingGrid.ThingAt<Mineable>(cell) != null);
            oreDeposits.RemoveAll(d => !HasOre(d));
            revealedDeposits.RemoveAll(d => !HasOre(d));
            unrevealedDeposits.RemoveAll(d => !HasOre(d));
        }

        public new CompPowerTrader PowerComp => powerComp;
        public List<List<IntVec3>> UnrevealedDeposits => unrevealedDeposits;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;

            // Visual progress indicator gizmo
            var scannerComp = GetComp<CompEPGScanner>();
            if (scannerComp != null)
            {
                yield return new Gizmo_ScanProgress(this);
            }

            yield return new Command_Toggle
            {
                defaultLabel = "Auto-mine revealed deposits",
                defaultDesc = "When enabled, newly revealed deposits and a mine shaft will be automatically designated for mining.",
                icon = ContentFinder<Texture2D>.Get("UI/Auto_Mine_Icon", true),
                isActive = () => autoMineEnabled,
                toggleAction = () => autoMineEnabled = !autoMineEnabled
            };
            if (DebugSettings.godMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Dev: Reveal Random Ore Deposit",
                    defaultDesc = "Reveal a random ore deposit for testing.",
                    action = () => RevealRandomOreDeposit(Map)
                };
                yield return new Command_Action
                {
                    defaultLabel = "Dev: Reveal All Ore Deposits",
                    defaultDesc = "Reveal all ore deposits for testing.",
                    action = () => { while (RevealRandomOreDeposit(Map)) { } }
                };
            }
        }
    }
}
