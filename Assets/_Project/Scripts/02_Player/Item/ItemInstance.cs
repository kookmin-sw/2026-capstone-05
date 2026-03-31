[System.Serializable]
public class ItemInstance
{
    public ItemData Data { get; private set; }

    public ItemRotation currentRotation = ItemRotation.Deg0;

    public ItemInstance(ItemData data)
    {
        Data = data;
    }

    public void Rotate(bool isClockwise)
    {
        if (isClockwise)
        {
            int nextRotation = ((int)currentRotation + 1) % 4;
            currentRotation = (ItemRotation)nextRotation;
        }
        else
        {
            int nextRotation = ((int)currentRotation - 1 + 4) % 4;
            currentRotation = (ItemRotation)nextRotation;
        }
    }
}