using System;

namespace GeoWar.Game
{
    // Üst bardaki kaynak kutuları. Oyuncu raptiyeyle hangi kaynağın hangi kutuda duracağını seçer.
    public sealed class ResourceBar
    {
        public readonly int[] Items;

        public ResourceBar(int[] items)
        {
            Items = items;
        }

        // Seçilen kaynak zaten barda başka bir kutudaysa iki kutu yer değiştirir.
        public void Place(int index, int resource)
        {
            int other = Array.IndexOf(Items, resource);
            if (other >= 0)
            {
                Items[other] = Items[index];
            }

            Items[index] = resource;
        }

        public bool Contains(int resource)
        {
            return Array.IndexOf(Items, resource) >= 0;
        }
    }
}
