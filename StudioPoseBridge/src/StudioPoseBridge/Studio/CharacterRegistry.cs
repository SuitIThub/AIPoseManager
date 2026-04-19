using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace StudioPoseBridge.Game
{
    public static class CharacterRegistry
    {
        public static global::Studio.Studio GetStudio()
        {
            return global::Studio.Studio.Instance;
        }

        public static OCIChar GetById(int id)
        {
            var st = GetStudio();
            if (st == null) return null;
            if (!st.dicObjectCtrl.TryGetValue(id, out var oci)) return null;
            return oci as OCIChar;
        }

        public static List<CharacterDto> ListCharacters()
        {
            var result = new List<CharacterDto>();
            var st = GetStudio();
            if (st == null) return result;
            var select = st.treeNodeCtrl != null ? st.treeNodeCtrl.selectNode : null;
            foreach (var kv in st.dicObjectCtrl)
            {
                var oci = kv.Value as OCIChar;
                if (oci == null) continue;
                var name = oci.treeNodeObject != null ? oci.treeNodeObject.textName : "character";
                var pos = oci.charInfo != null ? oci.charInfo.transform.position : Vector3.zero;
                var selected = oci.treeNodeObject != null && select != null && oci.treeNodeObject == select;
                result.Add(new CharacterDto
                {
                    Id = kv.Key,
                    Name = name,
                    Sex = MapSex(oci.sex),
                    Selected = selected,
                    Position = new[] { pos.x, pos.y, pos.z }
                });
            }
            return result;
        }

        public static bool TrySelect(int id, out string error)
        {
            error = null;
            var oci = GetById(id);
            if (oci == null)
            {
                error = "character not found";
                return false;
            }
            var st = GetStudio();
            if (st?.treeNodeCtrl == null || oci.treeNodeObject == null)
            {
                error = "tree not available";
                return false;
            }
            st.treeNodeCtrl.SelectSingle(oci.treeNodeObject, true);
            oci.OnSelect(true);
            SelectionState.SelectedCharacterId = id;
            return true;
        }

        private static string MapSex(int sex)
        {
            if (sex == 0) return "female";
            if (sex == 1) return "male";
            return "other";
        }
    }

    public sealed class CharacterDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Sex { get; set; }
        public bool Selected { get; set; }
        public float[] Position { get; set; }
    }
}
