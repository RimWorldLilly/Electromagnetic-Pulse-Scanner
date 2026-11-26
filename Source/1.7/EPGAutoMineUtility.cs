using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElectromagneticPulseGenerator
{
    public static class EPGAutoMineUtility
    {
        public static void DesignateDepositAndMineShaft(Map map, List<IntVec3> deposit)
        {
            HashSet<IntVec3> depositCells = new HashSet<IntVec3>(deposit);
            foreach (IntVec3 cell in deposit)
            {
                Mineable mineable = map.thingGrid.ThingAt<Mineable>(cell);
                if (mineable != null &&
                    mineable.def.building != null &&
                    mineable.def.building.isResourceRock &&
                    map.designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null)
                {
                    map.designationManager.AddDesignation(new Designation(mineable, DesignationDefOf.Mine));
                }
            }

            IntVec3? bestStart = null;
            IntVec3 bestEnd = IntVec3.Invalid;
            int bestNewMineables = int.MaxValue;
            int bestDistance = int.MaxValue;
            bool bestUsesExistingMineDesignation = false;

            IntVec3? closestGround = FindClosestUnfoggedCell(map, deposit);
            if (closestGround.HasValue)
            {
                ConsiderRoutes(
                    map,
                    closestGround.Value,
                    false,
                    deposit,
                    depositCells,
                    ref bestStart,
                    ref bestEnd,
                    ref bestNewMineables,
                    ref bestDistance,
                    ref bestUsesExistingMineDesignation);
            }

            foreach (IntVec3 cell in map.AllCells)
            {
                Mineable mineable = map.thingGrid.ThingAt<Mineable>(cell);
                if (mineable == null ||
                    depositCells.Contains(cell) ||
                    map.designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null)
                {
                    continue;
                }

                ConsiderRoutes(
                    map,
                    cell,
                    true,
                    deposit,
                    depositCells,
                    ref bestStart,
                    ref bestEnd,
                    ref bestNewMineables,
                    ref bestDistance,
                    ref bestUsesExistingMineDesignation);
            }

            if (!bestStart.HasValue)
            {
                return;
            }

            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(bestStart.Value, bestEnd))
            {
                if (depositCells.Contains(cell))
                {
                    continue;
                }

                Mineable mineable = map.thingGrid.ThingAt<Mineable>(cell);
                if (mineable != null &&
                    map.designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null)
                {
                    map.designationManager.AddDesignation(new Designation(mineable, DesignationDefOf.Mine));
                }
            }
        }

        private static void ConsiderRoutes(
            Map map,
            IntVec3 start,
            bool usesExistingMineDesignation,
            List<IntVec3> deposit,
            HashSet<IntVec3> depositCells,
            ref IntVec3? bestStart,
            ref IntVec3 bestEnd,
            ref int bestNewMineables,
            ref int bestDistance,
            ref bool bestUsesExistingMineDesignation)
        {
            foreach (IntVec3 end in deposit)
            {
                int newMineables = 0;
                foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(start, end))
                {
                    if (depositCells.Contains(cell))
                    {
                        continue;
                    }

                    if (map.thingGrid.ThingAt<Mineable>(cell) != null &&
                        map.designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null)
                    {
                        newMineables++;
                    }
                }

                int distance = Mathf.RoundToInt(start.DistanceTo(end));
                if (newMineables < bestNewMineables ||
                    (newMineables == bestNewMineables &&
                     usesExistingMineDesignation &&
                     !bestUsesExistingMineDesignation) ||
                    (newMineables == bestNewMineables &&
                     usesExistingMineDesignation == bestUsesExistingMineDesignation &&
                     distance < bestDistance))
                {
                    bestStart = start;
                    bestEnd = end;
                    bestNewMineables = newMineables;
                    bestDistance = distance;
                    bestUsesExistingMineDesignation = usesExistingMineDesignation;
                }
            }
        }

        private static IntVec3? FindClosestUnfoggedCell(Map map, List<IntVec3> deposit)
        {
            if (deposit == null || deposit.Count == 0)
                return null;

            // Expand outward from the deposit instead of rescanning every map cell
            // and measuring every deposit cell against it.
            HashSet<IntVec3> visited = new HashSet<IntVec3>(deposit);
            Queue<IntVec3> frontier = new Queue<IntVec3>(deposit);
            IntVec3? best = null;
            int bestDistanceSquared = int.MaxValue;
            bool bestIsStraight = false;
            int depth = 0;

            while (frontier.Count > 0)
            {
                int cellsAtDepth = frontier.Count;
                for (int i = 0; i < cellsAtDepth; i++)
                {
                    IntVec3 cell = frontier.Dequeue();
                    if (!map.fogGrid.IsFogged(cell) && cell.Standable(map))
                    {
                        int distanceSquared = int.MaxValue;
                        bool isStraight = false;
                        foreach (IntVec3 depositCell in deposit)
                        {
                            int dx = depositCell.x - cell.x;
                            int dz = depositCell.z - cell.z;
                            int candidateDistanceSquared = dx * dx + dz * dz;
                            if (candidateDistanceSquared < distanceSquared)
                                distanceSquared = candidateDistanceSquared;
                            if (depositCell.x == cell.x || depositCell.z == cell.z)
                                isStraight = true;
                        }

                        if (distanceSquared < bestDistanceSquared ||
                            (distanceSquared == bestDistanceSquared && isStraight && !bestIsStraight))
                        {
                            best = cell;
                            bestDistanceSquared = distanceSquared;
                            bestIsStraight = isStraight;
                        }
                    }

                    foreach (IntVec3 offset in GenAdj.AdjacentCellsAndInside)
                    {
                        IntVec3 next = cell + offset;
                        if (next.InBounds(map) && visited.Add(next))
                            frontier.Enqueue(next);
                    }
                }

                // Every unvisited cell is at least depth + 1 cells away in
                // Chebyshev distance, which is a lower bound for Euclidean distance.
                if (best.HasValue && depth + 1 > Mathf.Sqrt(bestDistanceSquared))
                    break;

                depth++;
            }

            return best;
        }
    }
}
