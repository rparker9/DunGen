using DunGen.Graph.Core;

public sealed class IdAllocator
{
    private int _nextNode = 1;
    private int _nextEdge = 1;
    private int _nextInsertion = 1;
    private int _nextKey = 1;
    private int _nextGate = 1;
    private int _nextCycle = 1;

    public NodeId NewNode() => new NodeId(_nextNode++);
    public EdgeId NewEdge() => new EdgeId(_nextEdge++);
    public InsertionId NewInsertion() => new InsertionId(_nextInsertion++);
    public KeyId NewKey() => new KeyId(_nextKey++);
    public GateId NewGate() => new GateId(_nextGate++);
    public CycleId NewCycle() => new CycleId(_nextCycle++);
}