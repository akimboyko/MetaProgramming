#region sample01
var list = new List<int> { 1, 2, 3 };
 
// Criterion 1: within a using statement...
using (var e = list.GetEnumerator())
{
    // Criterion 2: ...which includes a closure...
    Func<int> closure = () => e.Current;
     
    // Criterion 3: ...a mutable value type...
    while (e.MoveNext())
    {
        Console.WriteLine(e.Current);
    }
}
#endregion

#region sample02
int x = 0;
 
Action incrementX = () => x += 1;
 
incrementX();
incrementX();
 
Console.WriteLine(x);
#endregion