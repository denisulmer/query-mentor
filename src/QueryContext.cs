public class QueryContext
{
    public string CommandText { get; set; }
    public double ExecutionTime { get; set; }
    public List<string> Tables { get; set; }
}
