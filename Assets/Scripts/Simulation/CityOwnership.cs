using System;

namespace GeoWar.Simulation
{
    // Şehir sahipliği: şehrin sahibi CityOwner'da durur, ülkelerin şehir ve slot listeleri ondan kurulur.
    // Listeler yalnızca şehir el değiştirince yeniden kurulur; günlük hesap bellek ayırmaz.
    public static class CityOwnership
    {
        public static void RebuildCountryLists(WorldState world)
        {
            int count = world.CountryCount;
            int[] cityStart = world.CountryCityStart;
            int[] slotStart = world.CountryFacilityStart;
            Array.Clear(cityStart, 0, count + 1);
            Array.Clear(slotStart, 0, count + 1);

            for (int city = 0; city < world.CityCount; city++)
            {
                int owner = world.CityOwner[city];
                cityStart[owner + 1]++;
                slotStart[owner + 1] += FacilityCommands.SlotCount(world, city);
            }

            for (int country = 0; country < count; country++)
            {
                cityStart[country + 1] += cityStart[country];
                slotStart[country + 1] += slotStart[country];
            }

            var nextCity = new int[count];
            var nextSlot = new int[count];
            Array.Copy(cityStart, nextCity, count);
            Array.Copy(slotStart, nextSlot, count);

            for (int city = 0; city < world.CityCount; city++)
            {
                int owner = world.CityOwner[city];
                world.CountryCities[nextCity[owner]++] = city;
                for (int slot = world.CitySlotStart[city]; slot < world.CitySlotStart[city + 1]; slot++)
                {
                    world.CountrySlots[nextSlot[owner]++] = slot;
                }
            }
        }

        public static void SetCityNeighbors(WorldState world, int[] start, int[] neighbors)
        {
            world.CityNeighborStart = start;
            world.CityNeighbors = neighbors;
        }

        // Komşu şehirlerin sahipleri farklıysa o iki ülkenin kara sınırı vardır. Şehir komşuluğu yoksa tablo olduğu gibi kalır.
        public static void RebuildLandNeighbors(WorldState world)
        {
            if (world.CityNeighbors.Length == 0)
            {
                return;
            }

            int count = world.CountryCount;
            Array.Clear(world.CountryLandNeighbor, 0, world.CountryLandNeighbor.Length);
            for (int city = 0; city < world.CityCount; city++)
            {
                int owner = world.CityOwner[city];
                int end = world.CityNeighborStart[city + 1];
                for (int i = world.CityNeighborStart[city]; i < end; i++)
                {
                    int other = world.CityOwner[world.CityNeighbors[i]];
                    if (other != owner)
                    {
                        world.CountryLandNeighbor[owner * count + other] = true;
                    }
                }
            }

            world.RoutesDirty = true;
        }
    }
}
