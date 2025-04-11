namespace Parser.Models;
public class Architecture {
    public Entity Entity { get; private set; } = default!;
    public string Name { get; private set; } = string.Empty;
    public List<Operation> Tasks { get; private set; } = new();
    public Architecture(Entity entity){
        Entity = entity;
    }
    public void SetName(string Name) => this.Name = Name;
}