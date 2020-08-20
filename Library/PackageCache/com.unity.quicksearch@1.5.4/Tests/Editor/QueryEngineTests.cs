using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Unity.QuickSearch
{
    class TestObject
    {
        public enum TestEnum
        {
            ValueA = 0,
            ValueB = 1,
            ValueC = 2
        }

        public int id;
        public string name;
        public bool active;
        public TestEnum state;
        public float value;
    }

    struct TestParam
    {
        public TestObject target;
        public float value;
    }

    internal class QueryEngineTests
    {
        private QueryEngine m_QE;

        const string k_CorrectName = "Charlie";
        const int k_CorrectId = 42;
        private readonly TestObject[] m_Data = new[]
        {
            new TestObject { id = k_CorrectId, name = k_CorrectName },
            new TestObject { id = 4, name = "Charles" },
            new TestObject { id = 28, name = "Bob" },
            new TestObject { id = 48, name = "Charleston" },
            new TestObject { id = 48, name = "My Name Is A Phrase" },
            new TestObject { id = 48, name = "My Name Is A Phrase Containing Bobby" },
            new TestObject { id = 48, name = "Phrase A Is My Name" },
        };

        [SetUp]
        public void PrepareData()
        {
            m_QE = new QueryEngine();
            Assert.DoesNotThrow(() =>
            {
                SetupQueryEngine(m_QE);
            });
        }

        public static void SetupQueryEngine(QueryEngine qe)
        {
            qe.AddFilter("i", o => ((TestObject)o).id);
            qe.AddFilter("n", o => ((TestObject)o).name, new[] { ":", "=", "!=" });
            qe.AddFilter("s", o => ((TestObject)o).state, new[] { "=", "!=", "<", ">", "<=", ">=" });
            qe.AddFilter("a", o => ((TestObject)o).active, new[] { "=", "!=" });
            qe.AddFilter("v", o => ((TestObject)o).value);
            qe.AddFilter<string>("is", IsFilterResolver);
            qe.AddFilter<int, int>("inc", ((o, context) => ((TestObject)o).id + context));
            qe.AddOperatorHandler("=", (TestObject.TestEnum ev, TestObject.TestEnum fv) => ev == fv);
            qe.AddOperatorHandler("!=", (TestObject.TestEnum ev, TestObject.TestEnum fv) => ev != fv);
            qe.AddOperatorHandler("<", (TestObject.TestEnum ev, TestObject.TestEnum fv) => ev < fv);
            qe.AddOperatorHandler(">", (TestObject.TestEnum ev, TestObject.TestEnum fv) => ev > fv);
            qe.AddOperatorHandler("<=", (TestObject.TestEnum ev, TestObject.TestEnum fv) => ev <= fv);
            qe.AddOperatorHandler(">=", (TestObject.TestEnum ev, TestObject.TestEnum fv) => ev >= fv);
            qe.SetSearchDataCallback(o =>
            {
                var to = o as TestObject;
                return new[] { to.name, to.id.ToString() };
            });
        }

        [Test]
        public void Basic()
        {
            var query = m_QE.Parse("42 Charlie");
            ValidateNoErrors(query);

            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(k_CorrectId, ((TestObject)filteredValues[0]).id);
            Assert.AreEqual(k_CorrectName, ((TestObject)filteredValues[0]).name);

            query = m_QE.Parse("Bob");
            ValidateNoErrors(query);
            filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(2, filteredValues.Count);
            Assert.Contains(m_Data[2], filteredValues);
            Assert.Contains(m_Data[5], filteredValues);
        }

        [Test]
        public void Contains()
        {
            var query = m_QE.Parse("i:4 Cha");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.Contains(m_Data[0], filteredValues);
            Assert.Contains(m_Data[1], filteredValues);
            Assert.Contains(m_Data[3], filteredValues);
            Assert.IsFalse(filteredValues.Contains(m_Data[2]));
        }

        [Test]
        public void Lesser()
        {
            var query = m_QE.Parse("i<40");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.Contains(m_Data[1], filteredValues);
            Assert.Contains(m_Data[2], filteredValues);
            Assert.IsFalse(filteredValues.Contains(m_Data[0]));
        }

        [Test]
        public void And()
        {
            var query = m_QE.Parse("i>40 and i<45");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.Contains(m_Data[0], filteredValues);
            Assert.IsFalse(filteredValues.Contains(m_Data[1]));
            Assert.IsFalse(filteredValues.Contains(m_Data[2]));
        }

        [Test]
        public void Or()
        {
            var query = m_QE.Parse("(i>40 i<45) or n=Charles");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(2, filteredValues.Count);
            Assert.Contains(m_Data[0], filteredValues);
            Assert.Contains(m_Data[1], filteredValues);
            Assert.IsFalse(filteredValues.Contains(m_Data[2]));
        }

        [Test]
        public void Exact()
        {
            var query = m_QE.Parse("(i>40) !Charles");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsEmpty(filteredValues);

            query = m_QE.Parse("!Charles");
            ValidateNoErrors(query);
            filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.Contains(m_Data[1], filteredValues);
            Assert.IsFalse(filteredValues.Contains(m_Data[0]));
            Assert.IsFalse(filteredValues.Contains(m_Data[2]));
            Assert.IsFalse(filteredValues.Contains(m_Data[3]));
        }

        [Test]
        public void Not()
        {
            var query = m_QE.Parse("not Charles");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.Contains(m_Data[0], filteredValues);
            Assert.Contains(m_Data[2], filteredValues);
            Assert.IsFalse(filteredValues.Contains(m_Data[1]));
            Assert.IsFalse(filteredValues.Contains(m_Data[3]));

            query = m_QE.Parse("not !Charles");
            ValidateNoErrors(query);
            filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.Contains(m_Data[0], filteredValues);
            Assert.Contains(m_Data[2], filteredValues);
            Assert.Contains(m_Data[3], filteredValues);
            Assert.IsFalse(filteredValues.Contains(m_Data[1]));
        }

        [Test]
        public void Phrase()
        {
            var query = m_QE.Parse("My Name Is A Phrase");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(3, filteredValues.Count);
            Assert.Contains(m_Data[4], filteredValues);
            Assert.Contains(m_Data[5], filteredValues);
            Assert.Contains(m_Data[6], filteredValues);

            query = m_QE.Parse("\"My Name Is A Phrase\"");
            ValidateNoErrors(query);
            filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(2, filteredValues.Count);
            Assert.Contains(m_Data[4], filteredValues);
            Assert.Contains(m_Data[5], filteredValues);

            query = m_QE.Parse("!\"My Name Is A Phrase\"");
            ValidateNoErrors(query);
            filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(m_Data[4], filteredValues);
        }

        [Test]
        public void NestedGroups()
        {
            var query = m_QE.Parse("((i>0 i<10) or (i>20 i<40)) or n=Charleston");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(m_Data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(3, filteredValues.Count);
            Assert.Contains(m_Data[1], filteredValues);
            Assert.Contains(m_Data[2], filteredValues);
            Assert.Contains(m_Data[3], filteredValues);
        }

        [Test]
        public void Enums()
        {
            var data = new List<TestObject>
            {
                new TestObject { id = 1, name = "Seb", state = TestObject.TestEnum.ValueA },
                new TestObject { id = 2, name = "Jo", state = TestObject.TestEnum.ValueB },
                new TestObject { id = 3, name = "Phil", state = TestObject.TestEnum.ValueC },
            };
            var query = m_QE.Parse("s=ValueB");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);

            query = m_QE.Parse("s!=ValueB");
            ValidateNoErrors(query);
            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(2, filteredValues.Count);
            Assert.Contains(data[0], filteredValues);
            Assert.Contains(data[2], filteredValues);

            query = m_QE.Parse("s>ValueB");
            ValidateNoErrors(query);
            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[2], filteredValues);

            query = m_QE.Parse("s<=ValueC");
            ValidateNoErrors(query);
            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(3, filteredValues.Count);
            Assert.Contains(data[0], filteredValues);
            Assert.Contains(data[1], filteredValues);
            Assert.Contains(data[2], filteredValues);
        }

        [Test]
        public void Booleans()
        {
            var data = new List<TestObject>
            {
                new TestObject { id = 1, name = "Seb", active = false},
                new TestObject { id = 2, name = "Jo", active = true},
            };
            var query = m_QE.Parse("a=false");
            ValidateNoErrors(query);
            var filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[0], filteredValues);

            query = m_QE.Parse("a=true");
            ValidateNoErrors(query);
            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);

            query = m_QE.Parse("a!=false");
            ValidateNoErrors(query);
            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);
        }

        [Test]
        public void CustomFilterHandler()
        {
            var data = new List<TestObject>
            {
                new TestObject { id = 1, name = "name1", active = false, state = TestObject.TestEnum.ValueA, value = 10.0f},
                new TestObject { id = 2, name = "name2", active = true, state = TestObject.TestEnum.ValueB, value = 1000.0f},
            };

            var query = m_QE.Parse("is:rich");
            ValidateNoErrors(query);

            var filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);
        }

        [Test]
        public void CustomOperator()
        {
            var data = new List<TestObject>
            {
                new TestObject { id = 1, name = "name1", active = false, state = TestObject.TestEnum.ValueA, value = 10.0f},
                new TestObject { id = 2, name = "name2", active = true, state = TestObject.TestEnum.ValueB, value = 1000.0f},
            };

            const string op = "%";
            m_QE.AddOperator(op);
            m_QE.AddOperatorHandler(op, (int ev, int fv) => ev % fv == 0);

            var query = m_QE.Parse("i%2");
            ValidateNoErrors(query);

            var filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);

            query = m_QE.Parse("n%2");
            Assert.IsFalse(query.valid);
        }

        [Test]
        public void CustomTypeParser()
        {
            var data = new List<TestObject>
            {
                new TestObject { id = 1, name = "name1", active = false, state = TestObject.TestEnum.ValueA, value = 10.0f},
                new TestObject { id = 2, name = "name2", active = true, state = TestObject.TestEnum.ValueB, value = 1000.0f},
                new TestObject { id = 42, name = "name3", active = false, state = TestObject.TestEnum.ValueC, value = 100.0f}
            };

            m_QE.AddTypeParser<List<int>>(s =>
            {
                var tokens = s.Split(',');
                if (tokens.Length == 0)
                    return new ParseResult<List<int>>(false, null);

                var numberList = new List<int>(tokens.Length);
                foreach (var token in tokens)
                {
                    if (Utils.TryConvertValue(token, out int number))
                    {
                        numberList.Add(number);
                    }
                    else
                        return new ParseResult<List<int>>(false, null);
                }

                return new ParseResult<List<int>>(true, numberList);
            });

            var op = "?";
            m_QE.AddOperator(op);
            m_QE.AddOperatorHandler(op, (int ev, List<int> values) => values.Contains(ev));

            var query = m_QE.Parse("i?1,8,42");
            ValidateNoErrors(query);

            var filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(2, filteredValues.Count);
            Assert.Contains(data[0], filteredValues);
            Assert.Contains(data[2], filteredValues);

            m_QE.AddTypeParser<Vector2>(s =>
            {
                if (!s.StartsWith("[") || !s.EndsWith("]"))
                    return new ParseResult<Vector2>(false, Vector2.zero);

                var trimmed = s.Trim('[', ']');
                var vectorTokens = trimmed.Split(',');
                var vectorValues = vectorTokens.Select(token => float.Parse(token, CultureInfo.InvariantCulture.NumberFormat)).ToList();
                Assert.AreEqual(vectorValues.Count, 2);
                var vector = new Vector2(vectorValues[0], vectorValues[1]);
                return new ParseResult<Vector2>(true, vector);
            });
            m_QE.AddOperatorHandler(">", (float ev, Vector2 fv) => ev > fv.magnitude);

            query = m_QE.Parse("v>[500,500]");
            ValidateNoErrors(query);

            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);
        }

        [Test]
        public void FilterWithParameter()
        {
            // Simple
            var data = new List<TestObject>
            {
                new TestObject { id = 1, name = "name1", active = false, state = TestObject.TestEnum.ValueA, value = 10.0f},
                new TestObject { id = 2, name = "name2", active = true, state = TestObject.TestEnum.ValueB, value = 1000.0f},
                new TestObject { id = 3, name = "Name With Spaces", active = true, state = TestObject.TestEnum.ValueC, value = 100.0f},
            };
            var query = m_QE.Parse("inc(1)=3");

            ValidateNoErrors(query);

            var filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);

            // With transform function
            m_QE.AddFilter("dist", ((o, context) => Mathf.Abs(((TestObject)o).value - context.value)), param =>
            {
                foreach (var testObject in data)
                {
                    if (testObject.name == param)
                        return new TestParam() {target = testObject, value = testObject.value};
                }
                return default(TestParam);
            });
            query = m_QE.Parse("dist(name2)<10.0");

            ValidateNoErrors(query);

            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(1, filteredValues.Count);
            Assert.Contains(data[1], filteredValues);

            // Multiple params
            m_QE.AddFilter("func", ((o, context) => Math.Abs(((TestObject)o).value - context.value) < Mathf.Epsilon || ((TestObject)o).state == context.target.state), param =>
            {
                var testParam = new TestParam();
                if (string.IsNullOrEmpty(param))
                    return testParam;
                var paramTokens = param.Split(';');
                var targetName = paramTokens[0].Trim('"');
                testParam.value = float.Parse(paramTokens[1], CultureInfo.InvariantCulture.NumberFormat);
                foreach (var testObject in data)
                {
                    if (testObject.name == targetName)
                        testParam.target = testObject;
                }
                return testParam;
            });
            query = m_QE.Parse("func(\"Name With Spaces\";10.0)=true");

            ValidateNoErrors(query);

            filteredValues = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredValues);
            Assert.AreEqual(2, filteredValues.Count);
            Assert.Contains(data[0], filteredValues);
            Assert.Contains(data[2], filteredValues);
        }

        [Test]
        public void StringComparisons()
        {
            var data = new List<TestObject>
            {
                new TestObject { id = 1, name = "name1", active = false, state = TestObject.TestEnum.ValueA, value = 10.0f},
                new TestObject { id = 2, name = "name2", active = true, state = TestObject.TestEnum.ValueB, value = 1000.0f},
                new TestObject { id = 3, name = "Name With Upper Cases", active = true, state = TestObject.TestEnum.ValueC, value = 100.0f},
            };
            var query = m_QE.Parse("n:name");
            ValidateNoErrors(query);

            var filteredData = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredData);
            Assert.Contains(data[0], filteredData);
            Assert.Contains(data[1], filteredData);
            Assert.Contains(data[2], filteredData);

            // Change the default string comparison options
            m_QE.SetGlobalStringComparisonOptions(StringComparison.Ordinal);
            query = m_QE.Parse("n:name");
            ValidateNoErrors(query);

            filteredData = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredData);
            Assert.Contains(data[0], filteredData);
            Assert.Contains(data[1], filteredData);
            Assert.IsFalse(filteredData.Contains(data[2]));

            // Add a filter that overrides the default string comparison options
            m_QE.AddFilter("name", o => ((TestObject)o).name, StringComparison.OrdinalIgnoreCase, new []{":", "=", "!="});
            query = m_QE.Parse("name:name");
            ValidateNoErrors(query);

            filteredData = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredData);
            Assert.Contains(data[0], filteredData);
            Assert.Contains(data[1], filteredData);
            Assert.Contains(data[2], filteredData);

            // Word matching should use the default string comparison options
            query = m_QE.Parse("name");
            ValidateNoErrors(query);

            filteredData = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredData);
            Assert.Contains(data[0], filteredData);
            Assert.Contains(data[1], filteredData);
            Assert.IsFalse(filteredData.Contains(data[2]));

            m_QE.SetGlobalStringComparisonOptions(StringComparison.OrdinalIgnoreCase);
            query = m_QE.Parse("name");
            ValidateNoErrors(query);

            filteredData = query.Apply(data).ToList();
            Assert.IsNotEmpty(filteredData);
            Assert.Contains(data[0], filteredData);
            Assert.Contains(data[1], filteredData);
            Assert.Contains(data[2], filteredData);
        }

        public static void ValidateNoErrors<T>(Query<T> query)
        {
            Assert.IsNotNull(query, "Query should not be null");
            if (!query.valid)
            {
                foreach (var queryError in query.errors)
                {
                    #if UNITY_2019_1_OR_NEWER
                    UnityEngine.Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, queryError.reason);
                    #else
                    Debug.LogWarning(queryError.reason);
                    #endif
                }
            }
            Assert.IsTrue(query.valid, "Query should be valid");
        }

        private static bool IsFilterResolver(object data, string op, string keyword)
        {
            if (op != ":")
                return false;

            var testObj = (TestObject)data;
            if (keyword.Equals("rich"))
                return testObj.active && testObj.state == TestObject.TestEnum.ValueB && testObj.value >= 500.0f;

            return false;
        }
    }

    internal class QueryGraphTests
    {
        private QueryEngine m_QE;

        [OneTimeSetUp]
        public void Init()
        {
            m_QE = new QueryEngine();
            Assert.DoesNotThrow(() =>
            {
                QueryEngineTests.SetupQueryEngine(m_QE);
            });
        }

        [Test]
        public void OptimizeSwapNot()
        {
            var query = m_QE.Parse("-i>20 n=Bob");
            QueryEngineTests.ValidateNoErrors(query);
            var graph = query.graph;
            Assert.DoesNotThrow(() =>
            {
                graph.Optimize(false, true);
            });
            Assert.IsFalse(graph.root.leaf);
            Assert.IsInstanceOf(typeof(AndNode), graph.root);
            Assert.IsInstanceOf(typeof(FilterNode), graph.root.children[0]);
            Assert.IsInstanceOf(typeof(NotNode), graph.root.children[1]);

            query = m_QE.Parse("n=Bob -i>20");
            QueryEngineTests.ValidateNoErrors(query);
            graph = query.graph;
            Assert.DoesNotThrow(() =>
            {
                graph.Optimize(false, true);
            });
            Assert.IsFalse(graph.root.leaf);
            Assert.IsInstanceOf(typeof(AndNode), graph.root);
            Assert.IsInstanceOf(typeof(FilterNode), graph.root.children[0]);
            Assert.IsInstanceOf(typeof(NotNode), graph.root.children[1]);

            query = m_QE.Parse("-(-i>20 n=Bob) or i>42");
            QueryEngineTests.ValidateNoErrors(query);
            graph = query.graph;
            Assert.DoesNotThrow(() =>
            {
                graph.Optimize(false, true);
            });
            Assert.IsFalse(graph.root.leaf);
            Assert.IsInstanceOf(typeof(OrNode), graph.root);
            Assert.IsInstanceOf(typeof(FilterNode), graph.root.children[0]);
            Assert.IsInstanceOf(typeof(NotNode), graph.root.children[1]);
            var notNode = graph.root.children[1];
            Assert.IsFalse(notNode.leaf);
            Assert.IsInstanceOf(typeof(AndNode), notNode.children[0]);
            var andNode = notNode.children[0];
            Assert.IsFalse(andNode.leaf);
            Assert.IsInstanceOf(typeof(FilterNode), andNode.children[0]);
            Assert.IsInstanceOf(typeof(NotNode), andNode.children[1]);
        }

        [Test]
        public void PropagateNotToLeaves()
        {
            var query = m_QE.Parse("-(a and b or c)");
            QueryEngineTests.ValidateNoErrors(query);
            var graph = query.graph;
            Assert.DoesNotThrow(() =>
            {
                graph.Optimize(true, false);
            });
            Assert.IsFalse(graph.root.leaf);
            Assert.IsInstanceOf(typeof(AndNode), graph.root);
            Assert.AreEqual(2, graph.root.children.Count);
            Assert.IsInstanceOf(typeof(OrNode), graph.root.children[0]);
            Assert.IsInstanceOf(typeof(NotNode), graph.root.children[1]);
            var orNode = graph.root.children[0];
            var notNode = graph.root.children[1];
            Assert.IsFalse(orNode.leaf);
            Assert.AreEqual(2, orNode.children.Count);
            Assert.IsInstanceOf(typeof(NotNode), orNode.children[0]);
            Assert.IsInstanceOf(typeof(SearchNode), orNode.children[0].children[0]);
            Assert.IsInstanceOf(typeof(NotNode), orNode.children[1]);
            Assert.IsInstanceOf(typeof(SearchNode), orNode.children[1].children[0]);
            Assert.IsInstanceOf(typeof(SearchNode), notNode.children[0]);
        }

        [Test]
        public void ReduceNotDepth()
        {
            var query = m_QE.Parse("---a");
            QueryEngineTests.ValidateNoErrors(query);
            var graph = query.graph;
            Assert.DoesNotThrow(() =>
            {
                graph.Optimize(false, false);
            });
            Assert.IsFalse(graph.root.leaf);
            Assert.IsInstanceOf(typeof(NotNode), graph.root);
            Assert.IsInstanceOf(typeof(SearchNode), graph.root.children[0]);

            query = m_QE.Parse("----a");
            QueryEngineTests.ValidateNoErrors(query);
            graph = query.graph;
            Assert.DoesNotThrow(() =>
            {
                graph.Optimize(false, false);
            });
            Assert.IsTrue(graph.root.leaf);
            Assert.IsInstanceOf(typeof(SearchNode), graph.root);
        }
    }

    internal class PerformanceTests
    {
        private static readonly int[] k_SampleSize = { 1000, 10000, 100000 };

        private QueryEngine m_QE;

        [OneTimeSetUp]
        public void Init()
        {
            m_QE = new QueryEngine();
            Assert.DoesNotThrow(() =>
            {
                QueryEngineTests.SetupQueryEngine(m_QE);
            });
        }

        [Test]
        public void BasicSearch([ValueSource(nameof(k_SampleSize))] int sampleSize)
        {
            var data = GenerateTestObjects(sampleSize);

            List<TestObject> filteredData = null;
            using (new DebugTimer("BasicSearch"))
            {
                var query = m_QE.Parse("a=true s=ValueB Dat");
                QueryEngineTests.ValidateNoErrors(query);

                filteredData = query.Apply(data).Cast<TestObject>().ToList();
            }

            Assert.IsNotEmpty(filteredData);
        }

        private List<TestObject> GenerateTestObjects(int number)
        {
            var data = new List<TestObject>();
            for (var i = 0; i < number; ++i)
            {
                data.Add(new TestObject {id = i, name = $"Data {i}", active = i % 2 == 0, state = (TestObject.TestEnum)(i % 3)});
            }
            return data;
        }
    }
}

