namespace IxMilia.ModelExplorer
{
    public struct Triangle
    {
        public int V1 { get; }
        public int V2 { get; }
        public int V3 { get; }

        public Triangle(int v1, int v2, int v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
        }
    }
}
