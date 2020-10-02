using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class GraphSaveUtility
{
    private DialogueGraphView _targetGraphView;
    private DialogueContainer _containerCache;
    private List<Edge> edges => _targetGraphView.edges.ToList();
    private List<DialogueNode> nodes => _targetGraphView.nodes.ToList().Cast<DialogueNode>().ToList();
    public static GraphSaveUtility GetInstance(DialogueGraphView targetGraphView)
    {
        return new GraphSaveUtility
        {
            _targetGraphView = targetGraphView
        };
    }

    public void SaveGraph(string fileName)
    {
        if (!edges.Any()) return;

        var dialogueContainer = ScriptableObject.CreateInstance<DialogueContainer>();
        var connectedPorts = edges.Where(x => x.input.node != null).ToArray();
        foreach (var connectionPort in edges.Where(x => x.input.node != null))
        {
            var outputNode = connectionPort.output.node as DialogueNode;
            var inputNode = connectionPort.input.node as DialogueNode;

            dialogueContainer.nodeLinks.Add(new NodeLinkData
            {
                baseNodeGuid = outputNode.GUID,
                portName = connectionPort.output.portName,
                targetNodeGuid = inputNode.GUID
            });
        }


        foreach(var dialogueNode in nodes.Where(node => !node.entryPoint))
        {
            dialogueContainer.dialogueNodeData.Add(new DialogueNodeData
            {
                GUID = dialogueNode.GUID,
                dialogueText = dialogueNode.dialogueText,
                position = dialogueNode.GetPosition().position
            });
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        AssetDatabase.CreateAsset(dialogueContainer, $"Assets/Resources/{fileName}.asset");
        AssetDatabase.SaveAssets();
    }
    
    public void LoadGraph(string fileName)
    {
        _containerCache = Resources.Load<DialogueContainer>(fileName);
        if (_containerCache == null)
        {
            EditorUtility.DisplayDialog("File Not Found", "Target dialogue graph file does not exists", "OK");
            return;
        }

        ClearGraph();
        GenerateNodes();
        ConnectNodes();
    }

    private void ConnectNodes()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var connections = _containerCache.nodeLinks.Where(x => x.baseNodeGuid == nodes[i].GUID).ToList();

            for (int j = 0; j < connections.Count; j++)
            {
                var targetNodeGuid = connections[j].targetNodeGuid;
                var targetNode = nodes.First(x => x.GUID == targetNodeGuid);
                LinkNodes(nodes[i].outputContainer[j].Q<Port>(), (Port)targetNode.inputContainer[0]);
                targetNode.SetPosition(
                    new Rect(
                        _containerCache.dialogueNodeData.First(x => x.GUID == targetNodeGuid).position,
                        _targetGraphView.defaultNodeSize
                    )
                );


            }
        }
    }

    private void LinkNodes(Port output, Port input)
    {
        var tempEdge = new Edge
        {
            output = output,
            input = input
        };

        tempEdge?.input.Connect(tempEdge);
        tempEdge?.output.Connect(tempEdge);
        _targetGraphView.Add(tempEdge);
    }

    private void GenerateNodes()
    {
        foreach(var nodeData in _containerCache.dialogueNodeData)
        {
            var tempNode = _targetGraphView.CreateDialogueNode(nodeData.dialogueText, Vector2.zero);
            tempNode.GUID = nodeData.GUID;
            _targetGraphView.AddElement(tempNode);

            var nodePorts = _containerCache.nodeLinks.Where(x => x.baseNodeGuid == nodeData.GUID).ToList();
            nodePorts.ForEach(x => _targetGraphView.AddChoicePort(tempNode, x.portName));
        }
    }

    private void ClearGraph()
    {
        nodes.Find(x => x.entryPoint).GUID = _containerCache.nodeLinks[0].baseNodeGuid;

        foreach(var node in nodes.Where(x=>!x.entryPoint))
        {
            edges.Where(x => x.input.node == node).ToList().ForEach(edge => _targetGraphView.RemoveElement(edge));
            _targetGraphView.RemoveElement(node);
        }
    }
}
