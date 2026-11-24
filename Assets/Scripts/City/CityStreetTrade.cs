using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HighStakes.City
{
    internal sealed class CityStreetTrade
    {
        internal enum Reaction { Report, Ignore, Negotiate }
        internal enum Stage { Quantity, InitialOffer, FinalOffer }

        public Item Product { get; }
        public int Quantity { get; private set; } = 1;
        public Stage CurrentStage { get; private set; }
        public long Total => (long)unitPrice * Quantity;

        private readonly int available;
        private readonly int askingPrice;
        private readonly int buyerLimit;
        private int unitPrice;

        public static bool IsSaleProduct(Item item) => ProductItemFactory.IsDrugProduct(item) && item.basePrice > 0;

        public static Reaction RollReaction(CityActor actor)
        {
            if (actor.TryGetComponent<PoliceBehavior>(out var officer) && officer.personality != PolicePersonality.Corrupt)
                return Reaction.Report;

            float roll = Random.value;
            if (roll < 0.20f) return Reaction.Report;
            return roll < 0.48f ? Reaction.Ignore : Reaction.Negotiate;
        }

        public CityStreetTrade(Item product, int available)
        {
            Product = product;
            this.available = available;
            int market = Mathf.Max(1, ProductItemFactory.GetMarketPrice(product));
            unitPrice = PriceAt(market, 0.82);
            askingPrice = Math.Max(unitPrice + 1, PriceAt(market, 1.15));
            buyerLimit = PriceAt(market, Random.Range(0.82f, 1.26f));
            CurrentStage = available > 1 ? Stage.Quantity : Stage.InitialOffer;
        }

        private static int PriceAt(int market, double factor) => (int)Math.Clamp(Math.Round(market * factor), 1, int.MaxValue);

        public void SelectQuantity(bool wholeStack)
        {
            Quantity = wholeStack ? available : 1;
            CurrentStage = Stage.InitialOffer;
        }

        // A counter within the buyer's budget settles the deal immediately.
        public bool MakeCounteroffer()
        {
            unitPrice = Mathf.Clamp(askingPrice, unitPrice, Mathf.Max(unitPrice, buyerLimit));
            CurrentStage = Stage.FinalOffer;
            return unitPrice == askingPrice;
        }

        public Conversation BuildConversation(CityActor actor)
        {
            string item = LocalizationManager.GetItemName(Product);
            string speaker = LocalizationManager.GetCityActorName(actor.id, actor.displayName);
            string leave = LocalizationManager.Get("ui.street_trade.leave");
            var conversation = new Conversation { id = "street_trade", speaker = speaker };
            if (CurrentStage == Stage.Quantity)
            {
                conversation.text = LocalizationManager.Get("ui.street_trade.quantity", "item", item);
                conversation.choices = new[] {
                    new StoryChoice { text = LocalizationManager.Get("ui.street_trade.one") },
                    new StoryChoice { text = LocalizationManager.Get("ui.street_trade.all", "count", available.ToString()) },
                    new StoryChoice { text = leave }
                };
                return conversation;
            }

            string key = CurrentStage == Stage.InitialOffer ? "ui.street_trade.initial_offer" : "ui.street_trade.final_offer";
            conversation.text = LocalizationManager.Get(key, "item", item, "count", Quantity.ToString(), "price", Total.ToString("N0"));
            var accept = new StoryChoice { text = LocalizationManager.Get("ui.street_trade.accept", "price", Total.ToString("N0")) };
            var exit = new StoryChoice { text = leave };
            conversation.choices = CurrentStage == Stage.InitialOffer
                ? new[] { accept, new StoryChoice { text = LocalizationManager.Get("ui.street_trade.counter", "price", ((long)askingPrice * Quantity).ToString("N0")) }, exit }
                : new[] { accept, exit };
            return conversation;
        }
    }
}
