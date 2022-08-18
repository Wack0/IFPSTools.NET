using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

using IFPSLib;
using IFPSAsmLib;
using IFPSLib.Tests.Properties;

namespace IFPSLib.Tests
{
    [TestClass]
    public class ScriptTest
    {
        private static readonly string origB64 = Convert.ToBase64String(Resources.CompiledCode);
        private static readonly string origB64_22 = Convert.ToBase64String(Resources.CompiledCode_v22);

        [TestMethod]
        public void TestLoadSave()
        {
            // Load the script.
            var script = Script.Load(Resources.CompiledCode);
            // Ensure it's not null.
            Assert.IsNotNull(script);
            // For an official script (compiled by inno setup), the entrypoint is the first function.
            Assert.AreEqual(script.EntryPoint, script.Functions[0]);
            // Save the script.
            var savedBytes = script.Save();
            // Convert to base64 for later.
            var saved = Convert.ToBase64String(savedBytes);
            // Load the saved script.
            var scriptSaved = Script.Load(savedBytes);
            // Save again.
            var savedTwice = Convert.ToBase64String(scriptSaved.Save());
            // Ensure both saved scripts equal each other.
            Assert.AreEqual(saved, savedTwice);
            // Ensure the saved script equals the original.
            Assert.AreEqual(saved, origB64);
            // Ensure the disassemblies are equal.
            Assert.AreEqual(script.Disassemble(), scriptSaved.Disassemble());
        }

        [TestMethod]
        public void TestAsm()
        {
            var script = Assembler.Assemble(Resources.CompiledCodeDisasm);
            var savedB64 = Convert.ToBase64String(script.Save());
            Assert.AreEqual(savedB64, origB64);
        }

        [TestMethod]
        public void TestLoadSaveV22()
        {
            // Load the script.
            var script = Script.Load(Resources.CompiledCode_v22);
            // Ensure it's not null.
            Assert.IsNotNull(script);
            // For an official script (compiled by inno setup), the entrypoint is the first function.
            Assert.AreEqual(script.EntryPoint, script.Functions[0]);
            // Save the script.
            var savedBytes = script.Save();
            // Convert to base64 for later.
            var saved = Convert.ToBase64String(savedBytes);
            // Load the saved script.
            var scriptSaved = Script.Load(savedBytes);
            // Save again.
            var savedTwice = Convert.ToBase64String(scriptSaved.Save());
            // Ensure both saved scripts equal each other.
            Assert.AreEqual(saved, savedTwice);
            // Ensure the saved script equals the original.
            Assert.AreEqual(saved, origB64_22);
            // Ensure the disassemblies are equal.
            Assert.AreEqual(script.Disassemble(), scriptSaved.Disassemble());
        }

        [TestMethod]
        public void TestAsmV22()
        {
            var script = Assembler.Assemble(Resources.CompiledCodeDisasm_v22);
            var savedB64 = Convert.ToBase64String(script.Save());
            Assert.AreEqual(savedB64, origB64);
        }
    }
}
