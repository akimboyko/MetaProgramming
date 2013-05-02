using System.Collections.Generic;

public class Examples
{
    public void Function()
    {
        var x = 6;
        System.Math.Sqrt(x * 7);
    }

    public IEnumerable<string> Cases()
    {
        yield return "Foo"; 
    }

    public object FactoryMethod()
    {
        if (true)
            return new object();

        return null;
    }
}
