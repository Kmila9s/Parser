namespace Parser.Models;
public class Entity {
    public required string Name { get; set; }
    public HashSet<Variable> Ports { get; set; } = new();
    
}