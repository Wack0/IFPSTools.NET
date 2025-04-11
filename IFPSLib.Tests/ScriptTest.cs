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
        private void TestLoadSaveImpl(byte[] compiled)
        {
            // Convert to base64.
            string orig = Convert.ToBase64String(compiled);
            // Load the script.
            var script = Script.Load(compiled);
            // Ensure it's not null.
            Assert.IsNotNull(script);
            // For an official script (compiled by inno setup), the entrypoint is the first function.
            if (script.EntryPoint != null) Assert.AreEqual(script.EntryPoint, script.Functions[0]);
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
            Assert.AreEqual(saved, orig);
            // Ensure the disassemblies are equal.
            Assert.AreEqual(script.Disassemble(), scriptSaved.Disassemble());
        }

        private void TestAsmImpl(string disasm, byte[] compiled)
        {
            string orig = Convert.ToBase64String(compiled);
            var script = Assembler.Assemble(disasm);
            var savedB64 = Convert.ToBase64String(script.Save());
            Assert.AreEqual(savedB64, orig);
        }

        [TestMethod]
        public void TestLoadSave()
        {
            TestLoadSaveImpl(Resources.CompiledCode);
        }

        [TestMethod]
        public void TestAsm()
        {
            TestAsmImpl(Resources.CompiledCodeDisasm, Resources.CompiledCode);
        }

        [TestMethod]
        public void TestLoadSaveV22()
        {
            TestLoadSaveImpl(Resources.CompiledCode_v22);
        }

        [TestMethod]
        public void TestAsmV22()
        {
            TestAsmImpl(Resources.CompiledCodeDisasm_v22, Resources.CompiledCode_v22);
        }

        [TestMethod]
        public void TestLoadSaveIs()
        {
            TestLoadSaveImpl(Resources.TestIsInsn);
        }

        [TestMethod]
        public void TestAsmIs()
        {
            TestAsmImpl(Resources.TestIsInsnDisasm, Resources.TestIsInsn);
        }

        [TestMethod]
        public void TestLoadSaveFloat80()
        {
            TestLoadSaveImpl(Resources.CompiledCode_float80);
        }

        [TestMethod]
        public void TestAsmFloat80()
        {
            TestAsmImpl(Resources.CompiledCodeDisasm_float80, Resources.CompiledCode_float80);
        }
    }
}
