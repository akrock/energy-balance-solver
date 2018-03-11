using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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

            foreach(var c in tableLayoutPanel1.Controls)
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

        private List<int> AvailableValues = new List<int>();

        private class SequenceEquality : IEqualityComparer<int[]>
        {
            public bool Equals(int[] x, int[] y)
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

            public int GetHashCode(int[] obj)
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
            public int[] Base { get; set; }
            public int[] Intersections { get; set; }
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

        private class SumVector
        {
            public List<TextBox> Boxes = new List<TextBox>();
            public List<int> BoxIndexes = new List<int>();

            public int? Sum = null;
            
            public List<int[]> PossibleSolutions = new List<int[]>();

            public void AddSolutions(List<TextBox> flat, List<int> values, List<SumVector> otherVectors)
            {
                var combinations = GetCombination(new List<int>(), values, Boxes.Count);
                var acceptedCombos = combinations.Where(x => x.Sum() == Sum).ToList().Select(x => x.OrderBy(z => z).ToArray()).Distinct(new SequenceEquality()).ToList();

                var commonIntersections = BoxIndexes.Where(x => otherVectors.Any(z => z.BoxIndexes.Contains(x))).Select(x => BoxIndexes.IndexOf(x)).ToList();
                
                var total = new List<int[]>();
                
                Parallel.ForEach(acceptedCombos, ac =>
                {
                    var perms = Permutations(ac.ToArray()).Select(x => x.ToArray()).ToList().Distinct(new SequenceEquality()).ToList();

                    var onlyIntersectionDiffs = perms.Select(x => new BalancePermutations
                    {
                        Base = x,
                        Intersections = x.Where((z, i) => commonIntersections.Contains(i)).ToArray()
                    }).Distinct(new BalancePermutationsEqualityComparer()).Select(x => x.Base).ToList();

                    lock (total)
                    {
                        total.AddRange(onlyIntersectionDiffs);
                    }
                });
    
                PossibleSolutions = total;
            
            }

            static IEnumerable<List<int>> GetCombination(List<int> temp, List<int> list, int length)
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
        }

        public static IEnumerable<T[]> Permutations<T>(T[] values, int fromInd = 0)
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

        private void Solve_Click(object sender, EventArgs e)
        {
            //Hunt for the things we are trying to solve.
            var vectors = GetVectors();
            AvailableValues = new List<int>();

            foreach(var tb in textBoxes)
            {
                if (!string.IsNullOrWhiteSpace(tb.Text) && tb.BackColor != Color.LightGreen)
                    AvailableValues.Add(int.Parse(tb.Text));
            }
            
            Parallel.ForEach(vectors, (v) => v.AddSolutions(flatBoxes, AvailableValues, vectors.Where(x => x != v).ToList()));
            
            ReduceSolutionsBySingles(vectors);
            ReduceSolutionsByIntersections(vectors);
            
            //Sort them by least solutions to most.
            BruteForce(vectors);
            
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

        private void BruteForce(List<SumVector> vectors)
        {
            var singles = AvailableValues.Where(x => AvailableValues.Count(z => z == x) == 1).ToList();

            var bag = new ConcurrentBag<int?[]>();
            var outerCts = new CancellationTokenSource();
            var blockingTasks = new BlockingCollection<Task>(40);

            vectors = vectors.OrderBy(x => x.PossibleSolutions.Count).ToList();

            Task.Run(() =>
            {
                foreach (var vp in Permutations(vectors.ToArray()))
                {
                    var processString = $"Processing: {string.Join(",", vp.Select(x => vectors.IndexOf(x)))}";
                    Console.WriteLine(processString);
                    if (outerCts.IsCancellationRequested)
                        break;

                    var pTask = Task.Run(() => AttemptSolution(AvailableValues.ToList(), vp.ToList(), outerCts.Token).ContinueWith(t =>
                    {
                        Console.WriteLine($"Finished {processString}");
                        if (t.Result != null)
                        {
                            bag.Add(t.Result);
                            outerCts.Cancel();
                        }
                    }));

                    blockingTasks.Add(pTask);
                }

                blockingTasks.CompleteAdding();
            });

            try
            {
                foreach (var bt in blockingTasks.GetConsumingEnumerable())
                    bt.Wait();
            }
            catch (Exception ex) { }

            var solution = bag.FirstOrDefault();

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
            else
            {
                MessageBox.Show("Failed to find a solution. Check inputs or regen in game.");
            }

        }

        private async Task<int?[]> AttemptSolution(List<int> valuesLeft, List<SumVector> vectors, CancellationToken outerToken)
        {
            if (outerToken.IsCancellationRequested)
                return null;

            var v = vectors.FirstOrDefault();

            var bag = new ConcurrentBag<int?[]>();
            var innerCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);

            //var blockingTasks = new BlockingCollection<Task>(2500);
            var tasks = new List<Task>();
            //Task.Run(() =>
            //{
                foreach(var s in v.PossibleSolutions)
                {
                    if (innerCts.IsCancellationRequested)
                        break;

                    //blockingTasks.Add(SolveFirstLevel(s, valuesLeft, v, vectors.Skip(1).ToList(), innerCts.Token).ContinueWith(t =>
                    tasks.Add(SolveFirstLevel(s, valuesLeft, v, vectors.Skip(1).ToList(), innerCts.Token).ContinueWith(t =>
                    {
                        if (t.Result != null)
                        {
                            bag.Add(t.Result);
                            innerCts.Cancel();
                        }
                    }));
                }

                //blockingTasks.CompleteAdding();
            //});
            
            try
            {
                //await Task.WhenAll(blockingTasks.GetConsumingEnumerable().ToArray());
                await Task.WhenAll(tasks.ToArray());
            }
            catch(Exception ex) { }
            
            return bag.FirstOrDefault();
        }



        private async Task<int?[]> SolveFirstLevel(int[]s, List<int> valuesLeft, SumVector v,  List<SumVector> vectors, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;

            var tempSetValues = new int?[100];
            var tempValuesLeft = valuesLeft.ToList();
            
            bool solutionWorks = true;
            for (int i = 0; i < v.Boxes.Count; i++)
            {
                var val = s[i];
                var matrixIdx = v.BoxIndexes[i];
                var setValue = tempSetValues[matrixIdx];
                if (setValue.HasValue)
                {
                    if (val != setValue)
                    {
                        solutionWorks = false;
                        break;
                    }
                }
                else
                {
                    var toSet = val;
                    if (tempValuesLeft.Contains(toSet))
                    {
                        tempValuesLeft.Remove(toSet);
                        tempSetValues[matrixIdx] = toSet;
                    }
                    else
                    {
                        solutionWorks = false;
                        break;
                    }
                }
            }

            if (solutionWorks)
            {
                var rslt = await AttemptSolve(tempSetValues.ToArray(), tempValuesLeft.ToList(), vectors.FirstOrDefault(), vectors.Skip(1).ToList(), token);
                if (rslt != null)
                    return rslt;
            }

            return null;
        }


        private Task<int?[]> AttemptSolve(int?[] setValues, List<int> valuesLeft, SumVector v, List<SumVector> vectorsLeft, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return Task.FromResult<int?[]>(null);

            if (v == null)
                return Task.FromResult(setValues);

            foreach (var s in v.PossibleSolutions)
            {
                if (token.IsCancellationRequested)
                    return Task.FromResult<int?[]>(null);

                var tempSetValues = setValues.ToArray();
                var tempValuesLeft = valuesLeft.ToList();

                bool solutionWorks = true;
                for (int i = 0; i < v.Boxes.Count; i++)
                {
                    var val = s[i];
                    var matrixIdx = v.BoxIndexes[i];
                    var setValue = tempSetValues[matrixIdx];
                    if (setValue.HasValue)
                    {
                        if (val != setValue)
                        {
                            solutionWorks = false;
                            break;
                        }
                    }
                    else
                    {
                        var toSet = val;
                        if (tempValuesLeft.Contains(toSet))
                        {
                            tempValuesLeft.Remove(toSet);
                            tempSetValues[matrixIdx] = toSet;
                        }
                        else
                        {
                            solutionWorks = false;
                            break;
                        }
                    }
                }

                if (solutionWorks)
                {
                    var rslt = AttemptSolve(tempSetValues.ToArray(), tempValuesLeft.ToList(), vectorsLeft.FirstOrDefault(), vectorsLeft.Skip(1).ToList(), token);
                    if (rslt != null)
                        return rslt;
                }
            }

            return Task.FromResult<int?[]>(null);
        }

        
        private void ReduceSolutionsBySingles(List<SumVector> vectors)
        {
            var singles = AvailableValues.Where(x => AvailableValues.Count(z => z == x) == 1).ToList();

            foreach (var s in singles)
            {
                var alwaysUsed = vectors.Where(x => x.PossibleSolutions.All(sol => sol.Contains(s))).FirstOrDefault();
                if (alwaysUsed != null)
                {
                    foreach (var v in vectors)
                    {
                        if (v == alwaysUsed)
                            continue;

                        var boxIntersections = v.Boxes.Select(b => alwaysUsed.Boxes.IndexOf(b)).ToList();

                        var okSolutions = v.PossibleSolutions.Where(x =>
                       {
                           for (int i = 0; i < boxIntersections.Count; i++)
                           {
                               if (x.ElementAt(i) == s && (boxIntersections[i] == -1 || alwaysUsed.PossibleSolutions.All(z => z.ElementAt(boxIntersections[i]) != s)))
                                   return false;
                           }

                           return true;
                       }).ToList();

                        v.PossibleSolutions = okSolutions;
                    }

                }
            }
        }

        private void ReduceSolutionsByIntersections(List<SumVector> vectors)
        {

            //Sort them by least solutions to most.
            vectors = vectors.OrderBy(x => x.PossibleSolutions.Count).ToList();

            for(var j =0; j < vectors.Count; j++)
            {
                var v = vectors[j];
                var stillOkSolutions = new ConcurrentBag<int[]>();

                //find intersects
                var boxIntersections = v.Boxes.Select(b => vectors.Where(x => x != v && x.Boxes.IndexOf(b) > -1).Select(x => new { Vector = x, Index = x.Boxes.IndexOf(b) }).ToList()).ToList();

                Parallel.ForEach(v.PossibleSolutions, s =>
                {
                    var solutonOk = true;
                    for (int i = 0; i < v.Boxes.Count; i++)
                    {
                        var bi = boxIntersections[i];
                        var value = s[i];

                        if (!bi.All(x => x.Vector.PossibleSolutions.Any(z => z[x.Index] == value)))
                        {
                            solutonOk = false;
                            break;
                        }
                    }

                    if (solutonOk)
                        stillOkSolutions.Add(s);
                });

                v.PossibleSolutions = stillOkSolutions.ToList();
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
                                rowBoxes.Sum = int.Parse(text);
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
                                colBoxes.Sum = int.Parse(text);
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
            ClearTextBoxes(true);
        }
    }
}
