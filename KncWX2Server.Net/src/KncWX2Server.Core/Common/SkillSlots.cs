namespace KncWX2Server.Core;

/// <summary>
/// The legacy GSkillSlot row is exactly three 32-bit skill-slot values.
/// </summary>
public readonly record struct SkillSlots(int Slot01, int Slot02, int Slot03)
{
    public static SkillSlots Empty => default;

    public int this[int index] => index switch
    {
        0 => Slot01,
        1 => Slot02,
        2 => Slot03,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public void CopyTo(Span<int> destination)
    {
        if (destination.Length < 3)
            throw new ArgumentException("Destination must contain at least three elements.", nameof(destination));

        destination[0] = Slot01;
        destination[1] = Slot02;
        destination[2] = Slot03;
    }
}
