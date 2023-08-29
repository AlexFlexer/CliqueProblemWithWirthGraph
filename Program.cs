using System.Text;
using System.Text.RegularExpressions;

namespace CliqueProblemWithWirthGraph
{
    internal class Program
    {
        private const char DELIMITER_NODES = ',';
        private const char DELIMITER_EDGES = ';';

        static void Main(string[] args)
        {
            //RunTest();
            var graphData = string.Join("", args);
            if (graphData.EndsWith(';'))
                graphData = graphData.Remove(graphData.Length - 1);
#if DEBUG
            var edges = GenerateEdges();
            Console.WriteLine("Edges generated, looking for cliques...");
#else
            var edges = ValidateAndReturnEdges(graphData);
#endif
            if (edges.Count == 0)
            {
                HelpMe();
                return;
            }
            var graph = new Graph<string>(edges.ToArray());
#if DEBUG
            // todo: program goes in seemingly endless loop,
            //  it's true for any configuration of graph
            var cliques = graph.FindCliquesOfSize(10);
#else
            var cliques = graph.FindAllCliques();
#endif
            if (cliques.Count == 0) Console.WriteLine("No Cliques found!");
            else
            {
                Console.WriteLine("Found cliques:");
                foreach (var clique in cliques)
                {
                    foreach (var node in clique)
                        Console.Write(node + ", ");
                    Console.WriteLine();
                }
            }
        }

#if DEBUG
        private static IList<(string, string)> GenerateEdges()
        {
            var r = new Random();
            var startStr1 = "bbcfhswfcgbwevhfjw";
            var startStr2 = "huiwhfiwjhfbcwrueihbfcvw";
            var result = new HashSet<(string, string)> { (startStr1, startStr2) };
            var defined = new HashSet<string> { startStr1, startStr2 };
            for (int i = 0; i < 10_000; ++i)
            {
                var connectExisting = r.Next(0, 2) == 1;
                if (connectExisting)
                {
                    var n1 = defined.ElementAt(r.Next(0, defined.Count));
                    var n2 = defined.ElementAt(r.Next(0, defined.Count));
                    result.Add((n1, n2));
                }
                else
                {
                    var n1 = defined.ElementAt(r.Next(0, defined.Count));
                    var n2 = GenStr(r, r.Next(5, 30));
                    defined.Add(n2);
                    result.Add((n1, n2));
                }
            }
            return result.ToList();
        }

        private static string GenStr(Random r, int len)
        {
            if (len <= 0) return "";
            var alphabet = "abcdefghijklmnopqrstuvwxyz";
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; ++i)
                sb.Append(alphabet[r.Next(0, alphabet.Length)]);
            return sb.ToString();
        }
#endif

        private static IList<(string, string)> ValidateAndReturnEdges(string graphData)
        {
            var result = new List<(string, string)>();
            var delimitedEdges = graphData.Split(DELIMITER_EDGES);
            if (delimitedEdges.Length > 0)
            {
                foreach (var edge in delimitedEdges)
                {
                    var nodes = edge.Split(DELIMITER_NODES);
                    if (nodes.Length < 2)
                    {
                        result.Clear();
                        break;
                    }
                    nodes[0] = nodes[0].Trim();
                    nodes[1] = nodes[1].Trim();
                    if (nodes[0].Length == 0 || nodes[1].Length == 0)
                    {
                        result.Clear();
                        break;
                    }
                    result.Add((nodes[0], nodes[1]));
                }
            }
            return result;
        }

        private static void HelpMe()
        {
            Console.WriteLine("This program looks for cliques of all sizes in the graph." +
                "\nTo pass graph, just type like this: A{0}B{1}C{0}D{1} and so on. Here, A, B, C, D are the graph nodes," +
                "\n\tThey can be anything you want (numbers, characters, sentences. The only thing that matters is the delimiters." +
                "\nIn the output you will see something like this: A B, and every new set of clique nodes will start from new line.", 
                DELIMITER_NODES, DELIMITER_EDGES);
        }

        private static void RunTest()
        {
            // TODO: алгоритм странно себя ведет, если есть узлы, содержащие дуги на самих себя
            var graph = new Graph<int>(
                (0, 1),
                (0, 3),
                (0, 4),
                (3, 0),
                (3, 1),
                (3, 4),
                (4, 3),
                (4, 0),
                (4, 1),
                (1, 0),
                (1, 3),
                (1, 4)
            );
            foreach (var clique in graph.FindAllCliques())
            {
                foreach (var node in clique)
                    Console.Write(node + " ");
                Console.WriteLine();
            }
        }
    }

    // структура данных "ориентированный граф", реализующая структуру Никлауса Вирта,
    // смотри подробнее о ней здесь: https://it.kgsu.ru/C_DIN/din_0083.html
    public class Graph<T>
    {
        private int mCountNodes;
        private NodeMain mHead;

        // внимение: ребра не должны повторяться!
        public Graph(params (T, T)[] nodes)
        {
            foreach (var nodesRelation in nodes)
            {
                var node1 = FindByValueOrCreate(nodesRelation.Item1);
                var node2 = FindByValueOrCreate(nodesRelation.Item2);
                LinkNodes(node1, node2);
            }
        }

        public IList<ISet<T>> FindAllCliques()
        {
            var result = new List<ISet<T>>();
            foreach (int i in Enumerable.Range(2, mCountNodes - 1))
                foreach (var list in FindCliquesOfSize(i))
                    result.Add(list);
            return result;
        }

        public IList<ISet<T>> FindCliquesOfSize(int cliqueSize)
        {
            // клика должна иметь минимальный размер в 2 узла и максимальный равный количеству узлов графа
            if (cliqueSize < 2 || cliqueSize > mCountNodes)
                throw new Exception("cliqueSize should be at least 2 and not exceed total amount of nodes in the graph");

            // набор ребер, которые мы уже проверили, и они являются частью клики
            var visitedEdges = new HashSet<NodeAdjacent>();
            // список с кликами, который потом будет возвращен из функции
            var result = new List<ISet<T>>(/* TODO preallocate maximum space for all possible cliques? */);

            // ищем клики, проходя по каждому узлу графа
            for (var ptrNodes = mHead; ptrNodes != null; ptrNodes = ptrNodes.next)
            {
                // сначала получаем неизведанные ранее ребра, исходящие из данного узла
                var unvisitedEdges = GetUnvisitedEdges(ptrNodes, visitedEdges);
                // продолжаем что-либо делать только в случае, если у данного узла имеется достаточно
                // неисследованных ребер, иначе данный узел не стоит нашего внимания
                if (unvisitedEdges.Count >= cliqueSize - 1)
                {
                    // пробуем все комбинации ребер (из неизведанных) размера cliqueSize-1
                    var combinator = new EdgeCombinator(unvisitedEdges, cliqueSize - 1);
                    do
                    {
                        // берем текущую комбинацию,...
                        var currentComb = combinator.GetState();
                        // ... смотрим, можно является ли данный узел вместе с данной комбинацией ребер
                        // частью клики, ...
                        var newFoundCliqueEdges = ConstructClique(ptrNodes, currentComb);
                        // ... если да, то...
                        if (newFoundCliqueEdges.Any())
                        {
                            var newSet = new HashSet<T>();
                            newSet.Add(ptrNodes.key);

                            // ... добавляем данную комбинацию ребер в список посещенных ребер, ...
                            foreach (var edge in currentComb)
                            {
                                visitedEdges.Add(edge);
                                newSet.Add(edge.id.key);
                            }
                            // ... а также добавляем другие ребра из данной клики в список изученных
                            foreach (var edge in newFoundCliqueEdges)
                            {
                                visitedEdges.Add(edge);
                                newSet.Add(edge.id.key);
                            }

                            result.Add(newSet);

                            // если весь граф -- целая клика, нам не нужно выводить все вариации
                            // взаимного расположения его узлов
                            if (cliqueSize == mCountNodes)
                                break;
                        }
                        // иначе, просто идем дальше, к следующей комбинации ребер
                    } while (!combinator.Next());
                }

            }
            return result;
        }

        private NodeMain FindByValueOrCreate(T value)
        {
            var result = FindNodeByValue(value);
            if (result == null)
            {
                result = new NodeMain(value, 0, mHead, null);
                mHead = result;
                ++mCountNodes;
            }
            return result;
        }

        private void LinkNodes(NodeMain n1, NodeMain n2)
        {
            for (var ptr = n1.trail; ptr != null; ptr = ptr.next)
                if (ptr.id == n2) return;
            n1.trail = new NodeAdjacent(n2, n1.trail);
            ++n2.count;
        }

        private NodeMain? FindNodeByValue(T value)
        {
            for (var ptr = mHead; ptr != null; ptr = ptr.next)
                if (EqualityComparer<T>.Default.Equals(ptr.key, value))
                    return ptr; 
            return null;
        }

        private IList<NodeAdjacent> GetUnvisitedEdges(NodeMain node, ISet<NodeAdjacent> visitedSet)
        {
            var result = new List<NodeAdjacent>();
            for (var ptr = node.trail; ptr != null; ptr = ptr.next)
                if (!visitedSet.Contains(ptr))
                    result.Add(ptr);
            return result;
        }

        private ISet<NodeAdjacent> ConstructClique(NodeMain currentNode, ISet<NodeAdjacent> edges)
        {
            var result = new HashSet<NodeAdjacent>();

            var candidateNodes = new HashSet<NodeMain> { currentNode };
            foreach (var edge in edges)
                candidateNodes.Add(edge.id);

            foreach (var candidate in candidateNodes)
            {
                int occurences = 1;
                if (candidate == currentNode) continue;
                for (var ptrEdge = candidate.trail; ptrEdge != null; ptrEdge = ptrEdge.next)
                {
                    // узел указывает сам на себя
                    if (ptrEdge.id == candidate) continue;

                    if (candidateNodes.Contains(ptrEdge.id))
                        ++occurences;

                    if (ptrEdge.id == currentNode)
                        result.Add(ptrEdge);
                }
                // если хоть один узел графа не указывает на какой-то из узлов-кандидатов,
                // мы разрываем цикл и не возвращаем никаких ребер
                if (occurences < candidateNodes.Count)
                {
                    result.Clear();
                    break;
                }
            }

            return result;
        }

        private class NodeMain
        {
            // значение в вершине графа
            public readonly T key;
            // количество вершин, входящих в данную
            public int count;
            // следующая вершина графа в списке заголовочных узлов
            public readonly NodeMain next;
            // список смежности, представляет собой дуги, выходящие из данной вершины графа
            public NodeAdjacent trail;

            public NodeMain(T key, int count, NodeMain next, NodeAdjacent trail)
            {
                this.key = key;
                this.count = count;
                this.next = next;
                this.trail = trail;
            }
        }

        private class NodeAdjacent
        {
            // указатель на другую вершину графа
            public readonly NodeMain id;
            // указывает на дуговой узел, представляющий следующую дугу, выходящую из вершины графа (если такая дуга есть)
            public readonly NodeAdjacent next;

            public NodeAdjacent(NodeMain id, NodeAdjacent next)
            {
                this.id = id;
                this.next = next;
            }
        }

        // данный класс позволяет генерировать всевозможные комбинации ребер и навигировать по данным комбинациям,
        // тратя при этом минимум памяти
        /*
            TODO: оптимизировать все так, чтобы такие комбинации, как 403 и 430 тоже считались одинаковыми
         */
        private class EdgeCombinator
        {
            private IList<int> mIndices;
            private IList<NodeAdjacent> mEdges;

            public EdgeCombinator(IList<NodeAdjacent> edges, int combinationLength)
            {
                if (edges.Count < combinationLength)
                    throw new Exception("List of edges shoould have length equal to or greater than length of the combination");
                mIndices = new List<int>(combinationLength);
                while (--combinationLength >= 0)
                    mIndices.Add(combinationLength);
                mEdges = edges;
            }

            // возвращает true, если комбинации начались сначала
            public bool Next()
            {
                IncrementIndices();
                return IsInStartingState();
            }

            // возвращает текущую комбинацию ребер в виде ISet
            public ISet<NodeAdjacent> GetState()
            {
                var result = new HashSet<NodeAdjacent>();
                foreach (var index in mIndices)
                    result.Add(mEdges[index]);
                return result;
            }

            private void IncrementIndices()
            {
                var ptrIndices = mIndices.Count - 1;
                while (ptrIndices < mIndices.Count)
                {
                    if (mIndices[ptrIndices] >= mEdges.Count)
                        mIndices[ptrIndices] = 0;
                    else
                        ++mIndices[ptrIndices];

                    if (mIndices[ptrIndices] >= mEdges.Count && ptrIndices > 0)
                        --ptrIndices;
                    else if (!CheckRepetitionBackward(ptrIndices))
                        ++ptrIndices;
                }
            }

            private bool CheckRepetitionBackward(int index)
            {
                if (mIndices[index] >= mEdges.Count || mIndices[index] < 0)
                    return true;
                for (int i = index - 1; i >= 0; --i)
                    if (mIndices[i] == mIndices[index]) 
                        return true;
                return false;
            }

            private bool IsInStartingState()
            {
                int expectedIndex = mIndices.Count - 1;
                foreach (var index in mIndices)
                    if (expectedIndex-- != index)
                        return false;
                return true;
            }
        }
    }
}