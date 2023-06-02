// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using NUnit.Framework.Internal;
using Tomlyn.Model;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void TestSerializeObject()
        {
            MyClass obj = new(); // Replace MyClass with your class name
            object? serialized = Serializer.SerializeObject(obj);

            Assert.That(serialized, Is.Not.Null);
            Assert.That(serialized, Is.InstanceOf<Dictionary<string, object>>());
        }

        [Test]
        public void TestSerializeString()
        {
            const string str = "Hello, world!";
            object? serialized = Serializer.SerializeObject(str);

            Assert.That(serialized, Is.EqualTo(str));
        }

        [Test]
        public void TestSerializeInt()
        {
            int thisInt = new Random().Next(1, 65535);
            object? serialized = Serializer.SerializeObject(thisInt);

            Assert.That(serialized, Is.EqualTo(thisInt.ToString()));
        }

        [Test]
        public void TestSerializeGuid()
        {
            Guid guid = Guid.NewGuid();
            object? serialized = Serializer.SerializeObject(guid);

            Assert.That(serialized,
                        Is.EqualTo(guid.ToString()),
                        "Serialized value should be equal to the string representation" +
                        $" of the Guid,\r\nbut was {serialized.GetType()}"
            );
        }

        [Test]
        public void TestSerializeListOfGuid()
        {
            List<Guid> list = new() { Guid.NewGuid(), Guid.NewGuid() };
            object? serialized = Serializer.SerializeObject(list);

            Assert.That(serialized, Is.InstanceOf<List<object>>());
            CollectionAssert.AllItemsAreInstancesOfType((List<object>)serialized, typeof(string));
        }

        [Test]
        public void TestSerializeDictionaryOfGuidString()
        {
            Dictionary<Guid, string> dict = new()
            {
                { Guid.NewGuid(), "Value 1" },
                { Guid.NewGuid(), "Value 2" }
            };
            object? serialized = Serializer.SerializeObject(dict);

            Assert.That(serialized, Is.InstanceOf<Dictionary<object, object>>());
        }

        [Test]
        public void TestSerializeObjectRecursionProblems()
        {
            // Arrange
            var instance1 = new MyClass();
            instance1.NestedInstance = new MyNestedClass(instance1);
            instance1.GuidNestedClassDict = new Dictionary<Guid, List<MyNestedClass>>
            {
                { Guid.NewGuid(), new List<MyNestedClass> { new MyNestedClass(instance1) } }
            };

            var instance2 = new MyClass();
            instance2.NestedInstance = new MyNestedClass(instance2);
            instance2.GuidNestedClassDict = new Dictionary<Guid, List<MyNestedClass>>
            {
                { Guid.NewGuid(), new List<MyNestedClass> { new MyNestedClass(instance2), new MyNestedClass(instance2) } }
            };

            // Act & Assert
            Assert.That(HasStackOverflow(() => Serializer.SerializeObject(instance1)), Is.False, "Serialization should not cause a stack overflow");
            Assert.That(HasStackOverflow(() => Serializer.SerializeObject(new List<object> { instance1, instance2 })), Is.False, "Serialization should not cause a stack overflow");
        }

        private const int MaxRecursionDepth = 1000; // Set a maximum recursion depth

        private static bool HasStackOverflow(Action action)
        {
            int recursionDepth = 0;
            bool stackOverflow = false;

            try
            {
                // Hook into the unhandled exception event to capture any unhandled exceptions
                AppDomain.CurrentDomain.UnhandledException
                    += (sender, args) =>
                    {
                        if (args.ExceptionObject is StackOverflowException)
                        {
                            stackOverflow = true;
                        }
                    };

                // Execute the action inside a separate thread
                _ = ThreadPool.QueueUserWorkItem(
                    _ =>
                    {
                        try
                        {
                            // Call the recursive method
                            RecursiveMethod(action, ref recursionDepth);
                        }
                        catch
                        {
                            // Handle any exceptions thrown during execution
                        }
                    });

                // Wait for the execution to complete or timeout
                Thread.Sleep(TimeSpan.FromSeconds(5));

                // Check if the recursion depth exceeds the limit
                if (recursionDepth > MaxRecursionDepth)
                {
                    stackOverflow = true;
                }
            }
            catch
            {
                // Handle any exceptions thrown while monitoring the stack
            }

            return stackOverflow;
        }

        private static void RecursiveMethod(Action action, ref int recursionDepth)
        {
            recursionDepth++;

            if (recursionDepth > MaxRecursionDepth)
            {
                throw new StackOverflowException("Recursion depth exceeded the limit.");
            }

            action.Invoke();

            recursionDepth--;
        }

        [Test]
        public void TestSerializeClassInstances()
        {
            try
            {
                MyClass instance1 = new();
                instance1.NestedInstance = new MyNestedClass(instance1);
                instance1.GuidNestedClassDict = new Dictionary<Guid, List<MyNestedClass>>
                {
                    { Guid.NewGuid(), new List<MyNestedClass> { new MyNestedClass(instance1) } }
                };

                MyClass instance2 = new();
                instance2.NestedInstance = new MyNestedClass(instance2);
                instance2.GuidNestedClassDict = new Dictionary<Guid, List<MyNestedClass>>
                {
                    { Guid.NewGuid(), new List<MyNestedClass> { new MyNestedClass(instance2), new MyNestedClass(instance2) } }
                };
                object serializedTest = Serializer.SerializeObject(instance1);
                List<object> list = new() { instance1, instance2 };
                object serialized = Serializer.SerializeObject(list);

                Assert.That(serialized, Is.InstanceOf<List<object>>(), "The serialized object should be of type List<object>.");
                CollectionAssert.AllItemsAreInstancesOfType(
                    (List<object>)serialized,
                    typeof(Dictionary<string, object>),
                    "All items in the serialized list should be of type Dictionary<string, object>."
                );

                var deserializedComponent = new Component();
                deserializedComponent.DeserializeComponent((TomlObject)serialized);
                Assert.That(deserializedComponent, Is.Not.Null, "Deserialization should not return null.");

                var deserializedList = (List<object>)serialized;
                Assert.That(deserializedList, Is.Not.Null, "Deserialized object should be a List<object>.");
                Assert.That(deserializedList, Has.Count.EqualTo(list.Count), "The deserialized list should have the same count as the original list.");

                for (int i = 0; i < list.Count; i++)
                {
                    var originalObject = list[i] as MyClass;
                    var deserializedObject = deserializedList[i] as Dictionary<string, object>;
                    Assert.Multiple(
                        () =>
                        {
                            Assert.That(
                                deserializedObject,
                                Is.Not.Null,
                                "Each deserialized item should be a Dictionary<string, object>."
                            );
                            Assert.That(
                                originalObject,
                                Is.Not.Null,
                                nameof(originalObject) + " != null"
                            );
                        }
                    );

                    // Compare specific properties/fields of the original and deserialized objects
                    Assert.That(originalObject?.NestedInstance,
                                Is.Not.Null,
                                "originalObject.NestedInstance != null"
                    );
                    if (deserializedObject != null)
                    {
                        AssertPropertyEquality(
                            originalObject?.NestedInstance,
                            deserializedObject,
                            "NestedInstance");
                    }

                    // Verify uniqueness of serialized objects
                    var serializedObjects = new HashSet<object>();
                    VerifyUniqueSerialization(serialized, serializedObjects);
                }
            }
            catch (StackOverflowException)
            {
                Assert.Fail(
                    "Stack Overflow Exception occurred during serialization. Check for cyclic dependencies in the object graph.");
            }
            catch (Exception ex)
            {
                Assert.Fail(
                    $"Test failed with exception: {ex.GetType().Name}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}");
            }
        }

        private static void AssertPropertyEquality(object? expectedValue, Dictionary<string, object> deserializedObject, string propertyName)
        {
            if (expectedValue is null)
            {
                Assert.That(deserializedObject[propertyName], Is.Null, $"Property '{propertyName}' should be null after deserialization.");
                return;
            }

            Assert.That(deserializedObject.ContainsKey(propertyName), Is.True, $"Property '{propertyName}' is missing after deserialization.");
            object actualValue = deserializedObject[propertyName];

            Assert.That(actualValue, Is.EqualTo(expectedValue), $"Property '{propertyName}' does not match after deserialization.");
        }

        private static void VerifyUniqueSerialization(object serialized, HashSet<object> serializedObjects)
        {
            if (serialized is not Dictionary<string, object> serializedDict)
            {
                if (serialized is not List<object> serializedList)
                    return;

                foreach (object serializedItem in serializedList)
                {
                    VerifyUniqueSerialization(serializedItem, serializedObjects);
                }

                return;
            }

            foreach (object serializedValue in serializedDict.Values)
            {
                switch (serializedValue)
                {
                    case Dictionary<string, object> nestedDict:
                        VerifyUniqueSerialization(nestedDict, serializedObjects);
                        return;

                    case List<object> serializedList:
                        VerifyUniqueSerialization(serializedList, serializedObjects);
                        return;
                }

                if (!serializedObjects.Add(serializedValue))
                {
                    Assert.Fail($"Duplicate object found during serialization: {serializedValue.GetType().Name}");
                }
            }
        }

        // Add more tests for additional types here
    }

    public class MyClass
    {
        public MyNestedClass? NestedInstance { get; set; }
        public Dictionary<Guid, List<MyNestedClass>>? GuidNestedClassDict { get; set; }
    }

    public class MyNestedClass
    {
        public MyClass ParentInstance { get; set; }

        public MyNestedClass(MyClass parentInstance)
        {
            ParentInstance = parentInstance;
        }
    }
}