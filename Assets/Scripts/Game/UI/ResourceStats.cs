using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Bir kaynağın dört kutuluk özeti: stok, üretim, tüketim, net. Kaynak penceresi ve bilgi penceresi kullanır.
    public sealed class ResourceStats
    {
        private const float StatGap = 3f;

        private readonly GameSession _session;
        private readonly Label _stock;
        private readonly Label _produced;
        private readonly Label _consumed;
        private readonly Label _net;

        public ResourceStats(VisualElement parent, GameSession session)
        {
            _session = session;
            VisualElement stats = UiBuilder.Passive(parent, "gw-resource-stats");
            _stock = AddStat(stats, "Stok", 0f);
            _produced = AddStat(stats, "Üretim/gün", StatGap);
            _consumed = AddStat(stats, "Tüketim/gün", StatGap);
            _net = AddStat(stats, "Net/gün", StatGap);
        }

        public static Color NetColor(long net)
        {
            return net > 0 ? UiColors.Good : net < 0 ? UiColors.Warn : UiColors.Text;
        }

        public void Refresh(int resource)
        {
            long produced = _session.PlayerProducedMilli(resource);
            long consumed = _session.PlayerConsumedMilli(resource);
            long net = produced - consumed;

            _stock.text = NumberFormat.Amount(_session.PlayerStockMilli(resource));
            _produced.text = "+" + NumberFormat.Amount(produced);
            _consumed.text = NumberFormat.Amount(-consumed);
            _net.text = NumberFormat.SignedAmount(net);
            _net.style.color = NetColor(net);
        }

        private static Label AddStat(VisualElement parent, string caption, float marginLeft)
        {
            VisualElement cell = UiBuilder.Passive(parent, "gw-resource-stat");
            cell.style.marginLeft = marginLeft;
            UiBuilder.Text(cell, caption, "gw-stat-caption");
            return UiBuilder.Text(cell, "", "gw-stat-value", "gw-f-700");
        }
    }
}
