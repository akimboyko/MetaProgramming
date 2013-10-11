<Query Kind="Program" />

// Sample from
// Real-World Functional Programming
// With examples in F# and C#
// Tomas Petricek with Jon Skeet
// http://www.manning.com/petricek/

void Main()
{
    ReadInput().Dump("Option<T> read from input");
}

Option<int> ReadInput() {
    string s = Util.ReadLine();
    int parsed;
    if (Int32.TryParse(s, out parsed)) 
        return Option.Some(parsed);
    else 
        return Option.None<int>();
}

enum OptionType { Some, None };   
abstract class Option<T> {
    private readonly OptionType tag;
    protected Option(OptionType tag) {
        this.tag = tag;
    }
    public OptionType Tag { get { return tag; } }   
}

class None<T> : Option<T> {                       
    public None() : base(OptionType.None) { }
}

class Some<T> : Option<T> {                               
    public Some(T value) : base(OptionType.Some) {
          this.value = value;
    }
    private readonly T value;
    public T Value { get { return value; } }   
}

static class Option {                      
    public static Option<T> None<T>() {       
        return new None<T>();
    }
    public static Option<T> Some<T>(T value) {   
        return new Some<T>(value);
    }
}
