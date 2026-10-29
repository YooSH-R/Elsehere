using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A305.EditorTools.Tripo;
using Tripo3D.UnityBridge.Editor;
using UnityEditor;
using UnityEngine;

// Copy into an isolated Unity project's Editor assembly; never run against production art.
public static class TripoDccBridgeSmoke
{
    public static async void Run()
    {
        string result = Argument("-tripoResults");
        var server = new WebSocketSharpServer();
        try
        {
            string fixture = Argument("-tripoFixture");
            byte[] original = File.ReadAllBytes(fixture);
            server.Start();
            Require(server.IsRunning, "Bridge server did not start.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri("ws://127.0.0.1:60610/"), timeout.Token);
            string greeting = await Receive(client, timeout.Token);
            Require(greeting.Contains("handshake_ack"), "No Bridge handshake.");
            string fileId = Guid.NewGuid().ToString("N");
            string modelName = "BridgeSmokeFixture_" + fileId;
            byte[] header = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(
                    new FileTransferMessage
                    {
                        payload = new FileTransferPayload
                        {
                            fileId = fileId,
                            fileName = modelName,
                            fileType = "fbx",
                            chunkIndex = 0,
                            chunkTotal = 1,
                            chunkSize = original.Length,
                        },
                    }
                )
            );
            byte[] packet = new byte[header.Length + original.Length];
            Buffer.BlockCopy(header, 0, packet, 0, header.Length);
            Buffer.BlockCopy(original, 0, packet, header.Length, original.Length);
            await client.SendAsync(
                new ArraySegment<byte>(packet),
                WebSocketMessageType.Binary,
                true,
                timeout.Token
            );
            while (true)
            {
                string response = await Receive(client, timeout.Token);
                if (!response.Contains("import_complete"))
                    continue;
                Require(
                    JsonUtility.FromJson<ImportCompleteMessage>(response).payload.success,
                    response
                );
                break;
            }
            await client.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Smoke complete",
                timeout.Token
            );
            server.Stop();
            string modelPath = Directory
                .GetFiles("Assets/TripoModels", modelName + "*.fbx", SearchOption.AllDirectories)
                .Single();
            GameObject model = PrepareModel(modelPath);
            Inspect(model);
            var record = TripoWorkbenchAssets.CreateRecord(
                model,
                TripoAssetKind.Character,
                TripoAttachmentKind.Skinned
            );
            Require(
                record != null && record.sourceModel == model,
                "Workbench record was not linked."
            );
            string work = "UserSettings/DccBridgeSmoke/" + fileId;
            Directory.CreateDirectory(work);
            string blend = Path.Combine(work, "Editable.blend");
            var blender = new TripoBlenderBridge(Argument("-tripoBlender"));
            await blender.PrepareAsync(modelPath, blend, timeout.Token);
            byte[] blendBefore = File.ReadAllBytes(blend);
            string candidatePath =
                Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/Candidate.fbx";
            await blender.ExportAsync(blend, candidatePath, timeout.Token);
            Inspect(PrepareModel(candidatePath));
            Require(original.SequenceEqual(File.ReadAllBytes(fixture)), "Source fixture changed.");
            Require(
                blendBefore.SequenceEqual(File.ReadAllBytes(blend)),
                "Artist blend source changed."
            );
            File.WriteAllText(
                result,
                "PASS: official Bridge handshake, binary FBX transfer, Unity import, bones, weights, five blendshapes, workbench record, Blender roundtrip, source preservation. No Tripo API calls.\n"
            );
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            server.Stop();
            File.WriteAllText(result, "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static GameObject PrepareModel(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (UnityEditor.ModelImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.importBlendShapes = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void Inspect(GameObject model)
    {
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        Require(skin != null && skin.bones.Length > 0, "Missing skeleton or skin.");
        Require(
            skin.sharedMesh.boneWeights.Length == skin.sharedMesh.vertexCount,
            "Missing weights."
        );
        foreach (
            string name in new[]
            {
                "mouthSmileLeft",
                "eyeBlinkLeft",
                "viseme_AA",
                "viseme_FF",
                "viseme_TH",
            }
        )
            Require(skin.sharedMesh.GetBlendShapeIndex(name) >= 0, "Missing shape: " + name);
    }

    private static async Task<string> Receive(
        ClientWebSocket client,
        CancellationToken cancellation
    )
    {
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult received;
        do
        {
            received = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
            Require(
                received.MessageType != WebSocketMessageType.Close,
                "Bridge closed the connection."
            );
            data.Write(buffer, 0, received.Count);
        } while (!received.EndOfMessage);
        return Encoding.UTF8.GetString(data.ToArray());
    }

    private static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException(name);
        return args[index + 1];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
