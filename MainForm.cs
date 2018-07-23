using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnergyBalanceSolver
{
    public partial class MainForm : Form
    {
        TextBox[,] textBoxes = new TextBox[10,10];
        List<TextBox> flatBoxes = new List<TextBox>();

        public MainForm()
        {
            InitializeComponent();
            
            foreach (var c in tableLayoutPanel1.Controls)
            {
                var textBox = c as TextBox;
                if (textBox == null)
                    continue;

                var tbIndex = int.Parse(textBox.Name.Replace("textBox", "")) - 1;

                var row = tbIndex / 10;
                var col = tbIndex % 10;

                textBoxes[row, col] = textBox;
                textBox.Click += TextBox_Click;
            }

            foreach (var t in textBoxes)
                flatBoxes.Add(t);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text += $" v{version.Major}.{version.Minor}";
        }

        private void TextBox_Click(object sender, EventArgs e)
        {
            var tb = (TextBox)sender;

            if (Control.ModifierKeys == Keys.Shift)
            {
                if (tb.BackColor != Color.LightGreen)
                    tb.BackColor = Color.LightGreen;
                else
                    tb.BackColor = Color.White;
            }
        }

        private List<sbyte> AvailableValues = new List<sbyte>();

        private class SequenceEquality : IEqualityComparer<sbyte[]>
        {
            public static readonly SequenceEquality Default = new SequenceEquality();

            public bool Equals(sbyte[] x, sbyte[] y)
            {
                if (x.Length != y.Length)
                    return false;

                for(var z = 0; z < x.Length; z++)
                {
                    if (x[z] != y[z])
                        return false;
                }

                return true;
            }

            public int GetHashCode(sbyte[] obj)
            {
                var hash = 2166136261;
                const uint prime = 16777619;
                foreach(var i in obj)
                {
                    unchecked
                    {
                        var k = (uint)i.GetHashCode();
                        hash ^= k;
                        hash *= prime;
                    }
                }
                return (int) hash;
            }
        }
        
        public class BalancePermutations
        {
            public sbyte[] Base { get; set; }
            public sbyte[] Intersections { get; set; }
        }

        private class BalancePermutationsEqualityComparer : SequenceEquality, IEqualityComparer<BalancePermutations>
        {
            public BalancePermutationsEqualityComparer()
            {
            }

            public bool Equals(BalancePermutations x, BalancePermutations y)
            {
                return base.Equals(x.Intersections, y.Intersections);
            }

            public int GetHashCode(BalancePermutations obj)
            {
                return base.GetHashCode(obj.Intersections);
            }
        }

        public class KeyDictionary : IEquatable<KeyDictionary>
        {
            private object _lockObject;
            private int _solutions;

            public byte IndexPos { get; }
            public sbyte Value { get; }
            public SortedList<sbyte, KeyDictionary> Values { get; private set; }


            public KeyDictionary(byte index, sbyte value)
            {
                _lockObject = new object();
                IndexPos = index;
                _solutions = 0;
                Values = null;
                Value = value;
            }

            
            public int Solutions { get { return _solutions; } }
            
            public void SetSolution(sbyte[] s)
            {
                var dict = this;
                foreach(var i in s)
                {
                    Interlocked.Increment(ref dict._solutions);

                    lock (dict._lockObject)
                    {
                        if (dict.Values == null)
                            dict.Values = new SortedList<sbyte,KeyDictionary>();

                        if(!dict.Values.TryGetValue(i, out var next))
                            dict.Values[i] = next = new KeyDictionary((byte)(dict.IndexPos + 1), i);

                        dict = next;
                    }
                }
            }

            internal HashSet<sbyte> GetValuesAtIndex(int index)
            {
                if(index == IndexPos)
                {
                    return new HashSet<sbyte>(Values.Keys);
                }
                else if (index > IndexPos)
                {
                    var result = new HashSet<sbyte>();
                    foreach(var s in Values)
                    {
                        result.UnionWith(s.Value.GetValuesAtIndex(index));
                    }

                    return result;
                }

                return new HashSet<sbyte>();
            }

            internal int RemoveSolutionsAtIndex(sbyte solution, int index)
            {
                if(index == IndexPos)
                {
                    if(Values.TryGetValue(solution, out var removed))
                    {
                        Values.Remove(solution);
                        _solutions -= removed.Solutions;
                        return removed.Solutions;
                    }
                }
                else if (index > IndexPos)
                {
                    var result = 0;
                    var toRemove = new List<sbyte>();
                    foreach(var s in Values)
                    {
                        var removed = s.Value.RemoveSolutionsAtIndex(solution, index);
                        result += removed;
                        if (removed > 0 && s.Value.Solutions == 0)
                            toRemove.Add(s.Key);
                    }

                    //If we have no more solutions in this key remove it totally.
                    foreach (var key in toRemove)
                        Values.Remove(key);
                    
                    _solutions -= result;
                    return result;
                }

                return 0;
            }

            internal bool AllSolutionsUse(sbyte s)
            {
                if (Values == null)
                    return true;

                if (IndexPos == 0 && Values.ContainsKey(s))
                {
                    return Values.All(x => x.Key == s || x.Value.AllSolutionsUse(s));
                }
                else if( IndexPos > 0)
                {
                    //I did not start w/ the s value, but maybe one of my children has it.
                    return Values.ContainsKey(s) || Values.Any(x => x.Value.AllSolutionsUse(s));
                }
                
                return false;
            }

            public bool Equals(KeyDictionary other)
            {
                return Value == other.Value;
            }

            internal KeyDictionary FilterForState(sbyte?[] setValues, List<sbyte> availableValues)
            {
                var ret = new KeyDictionary(IndexPos, Value);
                if (IndexPos == setValues.Length)
                {
                    ret._solutions = 1;
                    return ret;
                }

                ret.Values = new SortedList<sbyte, KeyDictionary>();
                
                if (Values != null)
                {
                    var myVal = setValues[IndexPos];

                    foreach (var kd in Values.Values)
                    {
                        if (myVal.HasValue && kd.Value == myVal)
                        {
                            //Check to see if the next one passes
                            var found = kd.FilterForState(setValues, availableValues);
                            if (found.Solutions != 0)
                            {
                                ret.Values[kd.Value] = found;
                                ret._solutions += found._solutions;
                            }
                        }
                        else if (!myVal.HasValue)
                        {
                            var thisSolutionAvail = availableValues.ToList();
                            if (thisSolutionAvail.Remove(kd.Value))
                            {
                                var found = kd.FilterForState(setValues, thisSolutionAvail);
                                if (found.Solutions != 0)
                                {
                                    ret.Values[kd.Value] = found;
                                    ret._solutions += found._solutions;
                                }
                            }
                        }
                    }
                }

                return ret;
            }
        }

        private class SumVector
        {
            private Guid Id = Guid.NewGuid();
            private int _estimatedSolutionCount;

            public List<TextBox> Boxes = new List<TextBox>();
            public List<int> BoxIndexes = new List<int>();

            public int? Sum = null;
            
            public int FinalSolutionsCount => SolutionsPopulated ? _SolutionDictionary.Solutions : _estimatedSolutionCount;

            public bool SolutionsPopulated { get; private set; }

            private KeyDictionary _SolutionDictionary = new KeyDictionary(0, sbyte.MinValue);
            private Func<List<sbyte>, CancellationToken, KeyDictionary> _SolutionDictionaryPopulator = null;

            public KeyDictionary GetSolutionDictionary(sbyte?[] setValues, List<sbyte> availableValues, CancellationToken token)
            {
                var primary = _SolutionDictionary;

                var passValues = new sbyte?[BoxIndexes.Count]; 
                var available = availableValues.ToList();
                if (setValues != null)
                {
                    int i = 0;
                    foreach (var bi in BoxIndexes)
                    {
                        var v = setValues[bi];
                        passValues[i++] = v;
                        if (v.HasValue)
                            available.Add(v.Value);
                    }
                }

                //We skipped this one because it was just too massive.
                if (_SolutionDictionaryPopulator != null)
                {
                    primary = _SolutionDictionaryPopulator(available, token);
                }

                return primary;
                if (setValues == null)
                    return _SolutionDictionary;
                else
                    return primary.FilterForState(passValues, available);
            }

            public HashSet<sbyte> GetValuesAtIndex(int index)
            {
                return _SolutionDictionary.GetValuesAtIndex(index);
            }

            public void RemoveSolutionsAtIndex(sbyte solution, int index)
            {
                var reduced = _SolutionDictionary.RemoveSolutionsAtIndex(solution, index);
            }

            public void AddSolutions(List<TextBox> flat, List<sbyte> values, List<SumVector> otherVectors, CancellationToken cancellationToken)
            {
                var commonIntersections = BoxIndexes.Where(x => otherVectors.Any(z => z.BoxIndexes.Contains(x))).Select(x => BoxIndexes.IndexOf(x)).ToList(); 
                
                var combinations = GetCombination(new List<sbyte>(), values, Boxes.Count).Where(x => x.Select(z => (int)z).Sum() == Sum).ToList();
                var acceptedCombos = combinations.Select(x => x.OrderBy(z => z).ToArray()).Distinct(new SequenceEquality()).ToList();
                combinations = null;

                Console.WriteLine($"[{Sum}]: {acceptedCombos.Count} combos");

                if (Boxes.Count > 7 && acceptedCombos.Count >= 5000)
                {
                    _SolutionDictionaryPopulator = (valuesLeft, token) =>
                    {
                        var popCombos = GetCombination(new List<sbyte>(), valuesLeft, Boxes.Count).Where(x => x.Select(z => (int)z).Sum() == Sum).ToList();
                        var popAcceptedCombos = popCombos.Select(x => x.OrderBy(z => z).ToArray()).Distinct(new SequenceEquality()).ToList();
                        popCombos = null;

                        return PopulateSolutionDictionary(commonIntersections, popAcceptedCombos, token);
                    };

                    _estimatedSolutionCount = acceptedCombos.Count * Factorial(Boxes.Count);
                }
                else
                {
                    _SolutionDictionary = PopulateSolutionDictionary(commonIntersections, acceptedCombos, cancellationToken);
                    SolutionsPopulated = true;
                }
            }

            private int Factorial(int count)
            {
                if (count == 1)
                    return 1;

                return count * Factorial(count - 1);
            }

            private KeyDictionary PopulateSolutionDictionary(List<int> commonIntersections, List<sbyte[]> acceptedCombos, CancellationToken cancellationToken)
            {
                var kd = new KeyDictionary(0, sbyte.MinValue);
                
                Parallel.ForEach(acceptedCombos, (ac, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Break();
                    }
                    else
                    {
                        var onlyIntersectionDiffs = IntersectionPermuatations(ac.ToArray(), commonIntersections.ToArray());
 
                        Parallel.ForEach(onlyIntersectionDiffs, (i, innerState) =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                innerState.Break();
                            else
                                kd.SetSolution(i);
                        });

                    }
                });

                return kd;
            }
            
            private static IEnumerable<List<sbyte>> GetCombination(List<sbyte> temp, List<sbyte> list, int length)
            {
                if (temp.Count == length)
                {
                    yield return temp;
                }
                else
                {
                    while (list.Any())
                    {
                        var value = list.Take(1);
                        list = list.Skip(1).ToList();
                        foreach (var y in GetCombination(temp.Concat(value).ToList(), list, length))
                            yield return y;
                    }
                }
            }

            private static IEnumerable<sbyte[]> IntersectionPermuatations(sbyte[] values, int[] intersections)
            {
                if (intersections.Length == 0)
                {
                    //Add special case handling if we have no intersections,
                    //just return the single value.
                    yield return values;
                    yield break;
                }
              
                var valuesInOrder = values.OrderBy(x => x).ToList();
                var returnArray = new sbyte[values.Length];
                
                foreach (var combo in SumVector.GetCombination(new List<sbyte>(), valuesInOrder, intersections.Length))
                {
                    var valuesForPerm = valuesInOrder.ToList();

                    foreach (var c in combo)
                        valuesForPerm.Remove(c);

                    foreach (var comboPerm in Permutations(combo.ToArray(), 0))
                    {
                        var intersectionCounter = 0;
                        var nextIntersection = intersections[intersectionCounter];
                        var valueSelector = 0;
                        for (var i = 0; i < values.Length; i++)
                        {
                            sbyte value;
                            if (i == nextIntersection)
                            {
                                value = comboPerm[intersectionCounter++];
                                nextIntersection = intersectionCounter < intersections.Length ? intersections[intersectionCounter] : -1;
                            }
                            else
                            {
                                value = valuesForPerm[valueSelector++];
                            }

                            returnArray[i] = value;
                        }

                        yield return returnArray.ToArray();
                    }
                }
            }

            private static IEnumerable<T[]> Permutations<T>(T[] values, int fromInd = 0)
            {
                if (fromInd + 1 == values.Length)
                    yield return values;
                else
                {
                    foreach (var v in Permutations(values, fromInd + 1))
                        yield return v;

                    for (var i = fromInd + 1; i < values.Length; i++)
                    {
                        SwapValues(values, fromInd, i);
                        foreach (var v in Permutations(values, fromInd + 1))
                            yield return v;
                        SwapValues(values, fromInd, i);
                    }
                }
            }

            private static void SwapValues<T>(T[] values, int pos1, int pos2)
            {
                if (pos1 != pos2)
                {
                    T tmp = values[pos1];
                    values[pos1] = values[pos2];
                    values[pos2] = tmp;
                }
            }

            internal bool AllSolutionsUse(sbyte s)
            {
                return _SolutionDictionary.AllSolutionsUse(s);
            }

            private HashSet<Tuple<int,int>> trackedStates = new HashSet<Tuple<int, int>>();

            private int savedItterations = 0;

            internal bool TrackState(sbyte[] setValues, List<sbyte> valuesLeft)
            {
                var primaryHash = SequenceEquality.Default.GetHashCode(valuesLeft.ToArray());
                var secondaryHash = SequenceEquality.Default.GetHashCode(setValues);

                lock (trackedStates)
                {
                    var ret = trackedStates.Add(Tuple.Create(primaryHash, secondaryHash));
                    if(!ret)
                        savedItterations++;
                    return ret;
                }
            }
        }
        
        private Progress ProgressBar;

        private async void Solve_Click(object sender, EventArgs e)
        {
            //Dis-allow while in progress.
            if (ProgressBar != null)
                return;

            var userCts = new CancellationTokenSource();
            ProgressBar = new Progress(userCts);
            ProgressBar.Show(this);
            try
            {
                //Hunt for the things we are trying to solve.
                ProgressBar.UpdateLabel("Gathering user inputs.");
                var vectors = GetVectors();
                AvailableValues = new List<sbyte>();

                foreach (var tb in textBoxes)
                {
                    if (!string.IsNullOrWhiteSpace(tb.Text) && tb.BackColor != Color.LightGreen)
                        AvailableValues.Add(sbyte.Parse(tb.Text));
                }

                if(!vectors.All(v => v.Boxes.Count > 0))
                {
                    throw new Exception();
                }


                ProgressBar.UpdateLabel("Finding all possible solutions...");
                await Task.Run(() =>
                {

                    ProgressBar.TotalSolutions = vectors.Count;

                    Parallel.ForEach(vectors, (v, state) =>
                    {
                        if (userCts.Token.IsCancellationRequested)
                        {
                            state.Break();
                        }
                        else
                        {
                            v.AddSolutions(flatBoxes, AvailableValues, vectors.Where(x => x != v).ToList(), userCts.Token);
                            lock (this)
                            {
                                ProgressBar.CompletedCount++;
                            }
                        }
                    });
                }, userCts.Token);
                
                ProgressBar.UpdateLabel("Reduce possible soltuions....");
                await Task.Run(() =>
                {
                    int totalSolutions = 0;
                    int afterReduce = 0;
                    do
                    {
                        totalSolutions = vectors.Sum(x => x.SolutionsPopulated ? x.FinalSolutionsCount : 0);

                        ReduceSolutionsBySingles(vectors);
                        ReduceSolutionsByIntersections(vectors);

                        afterReduce = vectors.Sum(x => x.SolutionsPopulated ? x.FinalSolutionsCount : 0);
                        Console.WriteLine($"TotalSolutions: {totalSolutions} -> {afterReduce}");
                    } while (totalSolutions != afterReduce);
                }, userCts.Token);
                
                ProgressBar.SetDificulty(vectors.Sum(x => x.FinalSolutionsCount));
                


                ProgressBar.TotalSolutions = 0;
                ProgressBar.CompletedCount = 0;

                //Sort them by least solutions to most.
                await BruteForce(vectors, userCts.Token);
            }
            catch (Exception ex)
            {
                if (!(ex is TaskCanceledException))
                {
                    userCts.Cancel();
                    MessageBox.Show("An error happened, please make sure your inputs are valid numbers!.");
                }
            }
            finally
            {
                ProgressBar.Close();
                ProgressBar = null;
            }
        }

        private void ClearTextBoxes(bool all = false)
        {
            foreach(var tb in textBoxes)
            {
                if(all || tb.BackColor != Color.LightGreen)
                {
                    tb.Text = "";
                    tb.BackColor = Color.White;
                }
            }
        }

        private async Task BruteForce(List<SumVector> vectors, CancellationToken userToken)
        {
            var bag = new ConcurrentBag<int?[]>();
            var outerCts = CancellationTokenSource.CreateLinkedTokenSource(userToken);
            var blockingTasks = new BlockingCollection<Task>(20);
            
            var consumedPositions = new HashSet<int>();
            var primaryProcessingOrder = new List<SumVector>();
            var secondaryProcessingOrder = new List<SumVector>();

            var notComputed = vectors.Where(x => !x.SolutionsPopulated).OrderBy(x => x.FinalSolutionsCount).ToList();
            var tempVectors = vectors.Where(x => x.SolutionsPopulated).ToList();
            
            while (primaryProcessingOrder.Count < tempVectors.Count)
            {
                //remaining to add.
                var toAdd = tempVectors.Where(v => !primaryProcessingOrder.Contains(v)).OrderByDescending(x => consumedPositions.Count(z => x.BoxIndexes.Contains(z)))
                    .ThenBy(x => x.FinalSolutionsCount)
                    .ThenBy(x => x.BoxIndexes[0]).FirstOrDefault();

                foreach (var bi in toAdd.BoxIndexes)
                    consumedPositions.Add(bi);

                primaryProcessingOrder.Add(toAdd);
            }

            consumedPositions = new HashSet<int>();
            while (secondaryProcessingOrder.Count < tempVectors.Count)
            {
                //remaining to add.
                var toAdd = tempVectors.Where(v => !secondaryProcessingOrder.Contains(v)).OrderByDescending(x => consumedPositions.Count(z => x.BoxIndexes.Contains(z)))
                    .ThenBy(x => x.FinalSolutionsCount)
                    .ThenBy(x => x.BoxIndexes[0]).FirstOrDefault();

                foreach (var bi in toAdd.BoxIndexes)
                    consumedPositions.Add(bi);

                secondaryProcessingOrder.Add(toAdd);
            }

            primaryProcessingOrder.AddRange(notComputed);
            secondaryProcessingOrder.AddRange(notComputed);
            
            double totalPossibleSolutions = 0;
            foreach(var v in vectors)
            {
                if (totalPossibleSolutions == 0)
                    totalPossibleSolutions = v.FinalSolutionsCount;
                else
                    totalPossibleSolutions *= v.FinalSolutionsCount;
            }

            ProgressBar.TotalSolutions = totalPossibleSolutions;

            var t = Task.Run(() => AttemptSolution(AvailableValues.ToList(), primaryProcessingOrder, outerCts));
            var t2 = Task.Run(() => AttemptSolution(AvailableValues.ToList(), secondaryProcessingOrder, outerCts));
            var primarySolution = await t;
            var secondarySolution = await t2;

            var solution = primarySolution ?? secondarySolution;
            if (solution != null)
            {

                ClearTextBoxes();

                for (int i = 0; i < solution.Length; i++)
                {
                    var v = solution[i];
                    if (v.HasValue)
                    {
                        var tb = flatBoxes[i];
                        tb.Text = v.Value.ToString();
                        if (v.Value == 0)
                            tb.BackColor = Color.LightGoldenrodYellow;
                        else if (v.Value < 0)
                            tb.BackColor = Color.LightBlue;
                        else
                            tb.BackColor = Color.LightSalmon;
                    }
                }

            }
            else if(!userToken.IsCancellationRequested)
            {
                MessageBox.Show("Failed to find a solution. Check inputs or regen in game.");
            }
        }

        private void UpdateLabel(string text)
        {
            ProgressBar?.UpdateLabel(text);
        }
        
        private async Task<sbyte?[]> AttemptSolution(List<sbyte> valuesLeft, List<SumVector> vectors, CancellationTokenSource cts)
        {
            if (cts.Token.IsCancellationRequested)
                return null;

            var v = vectors.FirstOrDefault();
            
            var bag = new ConcurrentBag<sbyte?[]>();
           
            var blockingTasks = new BlockingCollection<Task>(500);
            var tasks = new List<Task>();

            UpdateLabel($"Attempting solutions...");

            var solutionDictionary = v.GetSolutionDictionary(null, valuesLeft, cts.Token);

            foreach(var s in solutionDictionary.Values.OrderBy(x => x.Value.Solutions))
            {
                tasks.Add(Task.Run(() =>
                {
                    var result = AttemptSolveOneSolutionGroup(null, valuesLeft, v, s, vectors.Skip(1).ToList(), cts.Token);
                    if (result != null)
                    {
                        bag.Add(result);
                        cts.Cancel();
                    }
                }, cts.Token));
            }
            
            try
            {
                await Task.WhenAll(tasks.ToArray());
            }
            catch(Exception ex) { }
            
            return bag.FirstOrDefault();
        }
        
        private bool EvaluateSolutionGroup(sbyte?[] setValues, List<sbyte> valuesLeft, int matrixIndex, sbyte value)
        {
            var setValue = setValues[matrixIndex];
            if (setValue.HasValue)
            {
                if (value != setValue)
                {
                    return false;
                }
            }
            else
            {
                if (valuesLeft.Contains(value))
                {
                    valuesLeft.Remove(value);
                    setValues[matrixIndex] = value;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
        
        private class EvalContext
        {
            public sbyte?[] SetValues { get; set; }
            public List<sbyte> ValuesLeft { get; set; }
            public KeyDictionary KeyGroup { get; set; }
            public int Position { get; set; }
        }

        private sbyte?[] EvalNextGroups(EvalContext context, SumVector v, List<SumVector> vectorsLeft, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;
            
            var stack = new Stack<EvalContext>();
            stack.Push(context);

            while(stack.Any())
            {
                if (token.IsCancellationRequested)
                    return null;

                var ctx = stack.Pop();

                if (ctx.Position >= v.Boxes.Count)
                {
                    //if (v.TrackState(ctx.SetValues, ctx.ValuesLeft))
                    //{
                        var rslt = AttemptSolve(ctx.SetValues, ctx.ValuesLeft, vectorsLeft.FirstOrDefault(), vectorsLeft.Skip(1).ToList(), token);
                        if (rslt != null)
                            return rslt;
                    //}
                    //else
                    //{
                    //    Console.WriteLine("ITT prevented due to state tracking.");
                    //}
                   
                    continue;
                }

                var boxIdx = v.BoxIndexes[ctx.Position];
                var setVal = ctx.SetValues[boxIdx];

                //Short-cut if we have values set.
                if (setVal.HasValue)
                {
                    if(ctx.KeyGroup.Values.TryGetValue(setVal.Value, out var next))
                    {
                        stack.Push(new EvalContext
                        {
                            //Can we get away with not duplicating these here??
                            SetValues = ctx.SetValues.ToArray(),
                            ValuesLeft = ctx.ValuesLeft.ToList(),
                            KeyGroup = next,
                            Position = ctx.Position + 1
                        });
                    }

                    continue;
                }
                else
                {
                    foreach (var k in ctx.KeyGroup.Values.OrderByDescending(c => c.Value.Solutions))
                    {
                        var tempSetValues = ctx.SetValues.ToArray();
                        var tempValuesLeft = ctx.ValuesLeft.ToList();

                        var works = EvaluateSolutionGroup(tempSetValues, tempValuesLeft, v.BoxIndexes[ctx.Position], k.Key);

                        if (works)
                        {
                            stack.Push(new EvalContext
                            {
                                SetValues = tempSetValues,
                                ValuesLeft = tempValuesLeft,
                                KeyGroup = k.Value,
                                Position = ctx.Position + 1
                            });
                        }
                    }
                }
            }

            return null;
        }

        private sbyte?[] AttemptSolveOneSolutionGroup(sbyte?[] setValues, List<sbyte> valuesLeft, SumVector v, KeyValuePair<sbyte, KeyDictionary> k, List<SumVector> vectorsLeft, CancellationToken token)
        {
            bool topLevel = false;
            if (token.IsCancellationRequested)
                return null;

            if (setValues == null)
            {
                topLevel = true;
                setValues = new sbyte?[100];
            }


            var primarySetValues = setValues.ToArray();
            var primaryValuesLeft = valuesLeft.ToList();


            //Interlocked.Add(ref ProgressBar.TotalSolutions, k.Value.Solutions);

            bool solutionWorks = EvaluateSolutionGroup(primarySetValues, primaryValuesLeft, v.BoxIndexes[0], k.Key);
            if (solutionWorks)
            {

                var rslt = EvalNextGroups(new EvalContext
                {
                    SetValues = primarySetValues,
                    ValuesLeft = primaryValuesLeft,
                    KeyGroup = k.Value,
                    Position = 1,
                }, v, vectorsLeft, token);

                if (rslt != null)
                    return rslt;

                double skipped = k.Value.Solutions;
                foreach (var notComputed in vectorsLeft)
                    skipped *= notComputed.FinalSolutionsCount;

                lock (this)
                {
                    ProgressBar.CompletedCount += skipped;
                }
                Console.WriteLine($"[{v.Sum}]: Eliminated sg {k.Key} - {vectorsLeft.Count} vectors left.");
            }
            else
            {
                double skipped = k.Value.Solutions;
                foreach (var notComputed in vectorsLeft)
                    skipped *= notComputed.FinalSolutionsCount;

                lock(this)
                {
                    ProgressBar.CompletedCount += skipped;
                }
            }

           
            //else
            //{
            //    Console.WriteLine("ITT Prevented!!");
            //}
            return null;
        }

        private sbyte?[] AttemptSolve(sbyte?[] setValues, List<sbyte> valuesLeft, SumVector v, List<SumVector> vectorsLeft, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;

            if (v == null)
                return setValues;

            sbyte?[] result = null;
            //for state we really only care about the view
            //of the board for this vector & the remaining ones.
            var boxesToTrack = vectorsLeft.Concat(new[] { v }).SelectMany(x => x.BoxIndexes).Distinct().ToList();
            var trackState = setValues.Where((x, i) => x.HasValue && boxesToTrack.Contains(i)).Select(x => x.Value).ToArray();

            if (v.TrackState(trackState, valuesLeft))
            {
                var solutionDictionary = v.GetSolutionDictionary(setValues, valuesLeft, token);

                if (solutionDictionary.Values != null)
                {
                    Parallel.ForEach(solutionDictionary.Values.OrderBy(x => x.Value.Solutions), (k, state) =>
                    {
                        var temp = AttemptSolveOneSolutionGroup(setValues, valuesLeft, v, k, vectorsLeft, token);
                        if (temp != null || token.IsCancellationRequested)
                        {
                            result = temp;
                            state.Break();
                        }
                    });
                }
            }
            else
            {
                double skipped = v.FinalSolutionsCount;
                foreach (var notComputed in vectorsLeft)
                    skipped *= notComputed.FinalSolutionsCount;

                lock (this)
                {
                    ProgressBar.CompletedCount += skipped;
                }
            }

            return result;
        }


        private void ReduceSolutionsBySingles(List<SumVector> vectors)
        {
            var singles = AvailableValues.Where(x => AvailableValues.Count(z => z == x) == 1).ToList();

            foreach (var s in singles)
            {
                var alwaysUsed = vectors.Where(x => x.AllSolutionsUse(s)).OrderBy(x => x.FinalSolutionsCount).ToList();
                if(alwaysUsed.Any())
                {
                    var acceptibleBoxes = alwaysUsed.SelectMany(x => x.Boxes).ToList();
                    foreach (var v in vectors)
                    {
                        if (!alwaysUsed.Contains(v)|| !v.SolutionsPopulated)
                            continue;
                        
                        var boxIntersections = v.Boxes.Select(b => acceptibleBoxes.IndexOf(b)).ToList();

                        if (alwaysUsed.Count == 1 || boxIntersections.All(x => x == -1))
                        {
                            for (int i = 0; i < boxIntersections.Count; i++)
                            {
                                if (boxIntersections[i] == -1)
                                    v.RemoveSolutionsAtIndex(s, i);
                            }
                        }
                    }
                }
            }
        }

        private void ReduceSolutionsByIntersections(List<SumVector> vectors)
        {

            //Sort them by least solutions to most.
           // vectors = vectors.OrderBy(x => x.PossibleSolutions.Count).ToList();
           
            for(var j =0; j < vectors.Count; j++)
            {
                var v = vectors[j];
                if (!v.SolutionsPopulated)
                    continue;

                
                //find intersects
                var boxIntersections = v.Boxes.Select(b => vectors.Where(x => x != v && x.SolutionsPopulated && x.Boxes.IndexOf(b) > -1).Select(x => new { Vector = x, Index = x.Boxes.IndexOf(b) }).ToList()).ToList();

                for(var i = 0; i < boxIntersections.Count; i++)
                {
                    var intersection = boxIntersections[i];
                    if (intersection.Any())
                    {
                        var possibleValues = v.GetValuesAtIndex(i);
                        //Think there should always only be one here?
                        foreach(var otherV in intersection)
                        {
                            var values = otherV.Vector.GetValuesAtIndex(otherV.Index);
                            
                            foreach(var toRemove in possibleValues.Where(val => !values.Contains(val)))
                            {
                                v.RemoveSolutionsAtIndex(toRemove, i);
                            }
                        }

                        
                    }
                }
                



                //Parallel.ForEach(v.PossibleSolutions, s =>
                //{
                //    var solutonOk = true;
                //    for (int i = 0; i < v.Boxes.Count; i++)
                //    {
                //        var bi = boxIntersections[i];
                //        var value = s[i];

                //        if (!bi.All(x => x.Vector.PossibleSolutions.Any(z => z[x.Index] == value)))
                //        {
                //            solutonOk = false;
                //            break;
                //        }
                //    }

                //    if (solutonOk)
                //        stillOkSolutions.Add(s);
                //});

                //v.PossibleSolutions = stillOkSolutions.ToList();
            }
        }

        private List<SumVector> GetVectors()
        {
            var vectors = new List<SumVector>();
            
            var rowBoxes = new SumVector();
            var colBoxes = new SumVector();
            //search rows...
            for(var y = 0; y < 10; y++)
            {
                for(var x = 0; x < 10; x++)
                {
                    var tbRow = textBoxes[y, x];
                    if(!string.IsNullOrWhiteSpace(tbRow.Text))
                    {
                        if(tbRow.BackColor != Color.LightGreen)
                        {
                            rowBoxes.Boxes.Add(tbRow);
                        }
                        else
                        {
                            var text = tbRow.Text.ToUpper().Trim();

                            if (!text.Contains("U") && !text.Contains("D"))
                            {
                                text = text.Replace("L", string.Empty).Replace("R", string.Empty);
                                rowBoxes.Sum = sbyte.Parse(text);
                            }else
                            {
                                //this one isn't for us.
                                rowBoxes = new SumVector();
                            }
                        }
                    }
                    else
                    {
                        if(rowBoxes.Sum.HasValue && rowBoxes.Boxes.Any())
                        {
                            foreach (var b in rowBoxes.Boxes)
                            {
                                rowBoxes.BoxIndexes.Add(flatBoxes.IndexOf(b));
                            }

                            vectors.Add(rowBoxes);

                        }
                       
                       rowBoxes = new SumVector();
                    }


                    var tbCol = textBoxes[x, y];

                    if (!string.IsNullOrWhiteSpace(tbCol.Text))
                    {
                        if (tbCol.BackColor != Color.LightGreen)
                        {
                            colBoxes.Boxes.Add(tbCol);
                        }
                        else
                        {
                            var text = tbCol.Text.ToUpper().Trim();

                            if (!text.Contains("L") && !text.Contains("R"))
                            {
                                text = text.Replace("U", string.Empty).Replace("D", string.Empty);
                                colBoxes.Sum = sbyte.Parse(text);
                            }
                            else
                            {
                                //this one isn't for us.
                                colBoxes = new SumVector();
                            }
                        }
                    }
                    else
                    {
                        if (colBoxes.Sum.HasValue && colBoxes.Boxes.Any())
                        {
                            foreach (var b in colBoxes.Boxes)
                            {
                                colBoxes.BoxIndexes.Add(flatBoxes.IndexOf(b));
                            }
                            
                            vectors.Add(colBoxes);
                        }
                       
                        colBoxes = new SumVector();
                    }
                }

                //Handle wrap to next row/column
                if(rowBoxes.Sum.HasValue && rowBoxes.Boxes.Any())
                {
                    foreach (var b in rowBoxes.Boxes)
                    {
                        rowBoxes.BoxIndexes.Add(flatBoxes.IndexOf(b));
                    }

                    vectors.Add(rowBoxes);
                    rowBoxes = new SumVector();
                }

                if (colBoxes.Sum.HasValue && colBoxes.Boxes.Any())
                {

                    foreach (var b in colBoxes.Boxes)
                    {
                        colBoxes.BoxIndexes.Add(flatBoxes.IndexOf(b));
                    }

                    vectors.Add(colBoxes);
                    colBoxes = new SumVector();
                }
            }

            return vectors;
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            //Dis-allow while in progress.
            if (ProgressBar != null)
                return;

            ClearTextBoxes(true);
        }
    }
}
