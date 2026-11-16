using System.Collections.Generic;

public interface INpcInventoryProvider
{
    List<InStockItem> GetInventory();
}
