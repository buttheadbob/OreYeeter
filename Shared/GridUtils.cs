using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRageMath;

namespace OreYeeter
{
    public static class GridUtils
    {
        /// <summary>
        /// The mechanical group (grid + rotor/piston/hinge subgrids, NOT connector docked grids) the character is
        /// looking at, or null. Uses the nearest block hit by a ray from the character's head.
        /// </summary>
        public static List<MyCubeGrid> FindLookedAtMechanicalGroup(IMyCharacter character, float range = 200)
        {
            if (character == null)
                return null;

            Matrix head = character.GetHeadMatrix(true, true, false);
            Vector3D start = head.Translation + head.Forward * 0.5f;
            Vector3D end = head.Translation + head.Forward * (range + 0.5f);
            var ray = new RayD(start, head.Forward);

            MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group best = null;
            double bestDistance = double.MaxValue;

            foreach (var group in MyCubeGridGroups.Static.Mechanical.Groups)
            {
                foreach (var node in group.Nodes)
                {
                    IMyCubeGrid grid = node.NodeData;
                    if (grid?.Physics == null || !ray.Intersects(grid.WorldAABB).HasValue)
                        continue;

                    Vector3I? hit = grid.RayCastBlocks(start, end);
                    if (!hit.HasValue)
                        continue;

                    double distance = (start - grid.GridIntegerToWorld(hit.Value)).Length();
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = group;
                    }
                }
            }

            if (best == null)
                return null;

            var grids = new List<MyCubeGrid>();
            foreach (var node in best.Nodes)
                grids.Add(node.NodeData);
            return grids;
        }
    }
}
