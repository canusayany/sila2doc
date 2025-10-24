using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 生成代码去重器 - 处理Tecan Generator生成的重复类型定义
    /// 
    /// Tecan Generator在生成多个特性的客户端代码时，会在每个*Dtos.cs文件中
    /// 重复定义共享的数据类型（如异常类、公共数据结构等），导致编译错误。
    /// 
    /// 本类负责：
    /// 1. 扫描所有生成的*Dtos.cs文件
    /// 2. 检测重复的类型定义（类、枚举、结构体、接口、委托）
    /// 3. 注释掉重复的定义，仅保留第一个出现的
    /// 4. 保持代码格式和注释
    /// </summary>
    public class GeneratedCodeDeduplicator
    {
        /// <summary>
        /// 对生成的客户端代码目录进行去重处理
        /// </summary>
        /// <param name="clientCodeDirectory">客户端代码目录路径</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>去重统计信息</returns>
        public DeduplicationResult DeduplicateGeneratedCode(
            string clientCodeDirectory,
            Action<string>? progressCallback = null)
        {
            var result = new DeduplicationResult();

            try
            {
                progressCallback?.Invoke("  → 开始检查重复的类型定义...");

                // 查找所有.cs文件（包括DTOs和接口文件）
                var allCsFiles = Directory.GetFiles(clientCodeDirectory, "*.cs", SearchOption.TopDirectoryOnly);
                if (allCsFiles.Length == 0)
                {
                    progressCallback?.Invoke("  ⚠ 未找到任何.cs文件");
                    return result;
                }

                progressCallback?.Invoke($"  → 找到 {allCsFiles.Length} 个.cs文件");

                // 第一遍：收集所有类型定义及其位置
                var typeDefinitions = CollectTypeDefinitions(allCsFiles, progressCallback);

                // 检测重复
                var duplicates = FindDuplicates(typeDefinitions);
                result.TotalTypesFound = typeDefinitions.Count;
                result.DuplicateTypesFound = duplicates.Count;

                if (duplicates.Count == 0)
                {
                    progressCallback?.Invoke("  ✓ 未发现重复的类型定义");
                    result.Success = true;
                    return result;
                }

                progressCallback?.Invoke($"  → 发现 {duplicates.Count} 个重复的类型");

                // 第二遍：注释掉重复的定义
                foreach (var (typeName, locations) in duplicates)
                {
                    // 保留第一个，注释掉其余的
                    var toComment = locations.Skip(1).ToList();
                    foreach (var location in toComment)
                    {
                        CommentOutTypeDefinition(location);
                        result.CommentedTypes.Add($"{typeName} in {Path.GetFileName(location.FilePath)}");
                    }
                }

                progressCallback?.Invoke($"  ✓ 已注释 {result.CommentedTypes.Count} 个重复的类型定义");
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                progressCallback?.Invoke($"  ✗ 去重处理失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 收集所有类型定义及其位置
        /// </summary>
        private List<TypeDefinitionInfo> CollectTypeDefinitions(
            string[] dtoFiles,
            Action<string>? progressCallback)
        {
            var allTypes = new List<TypeDefinitionInfo>();

            foreach (var filePath in dtoFiles)
            {
                var fileName = Path.GetFileName(filePath);
                progressCallback?.Invoke($"    分析: {fileName}");

                var code = File.ReadAllText(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);
                var root = syntaxTree.GetRoot();

                // 查找类定义（仅顶层类，不包括嵌套类）
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(c => c.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax ||
                               c.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax ||
                               c.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax);
                foreach (var classSyntax in classes)
                {
                    allTypes.Add(new TypeDefinitionInfo
                    {
                        TypeName = classSyntax.Identifier.Text,
                        TypeKind = "class",
                        FilePath = filePath,
                        Node = classSyntax,
                        StartLine = syntaxTree.GetLineSpan(classSyntax.Span).StartLinePosition.Line + 1
                    });
                }

                // 查找枚举定义（仅顶层枚举）
                var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>()
                    .Where(e => e.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax ||
                               e.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax ||
                               e.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax);
                foreach (var enumSyntax in enums)
                {
                    allTypes.Add(new TypeDefinitionInfo
                    {
                        TypeName = enumSyntax.Identifier.Text,
                        TypeKind = "enum",
                        FilePath = filePath,
                        Node = enumSyntax,
                        StartLine = syntaxTree.GetLineSpan(enumSyntax.Span).StartLinePosition.Line + 1
                    });
                }

                // 查找结构体定义（仅顶层结构体）
                var structs = root.DescendantNodes().OfType<StructDeclarationSyntax>()
                    .Where(s => s.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax ||
                               s.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax ||
                               s.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax);
                foreach (var structSyntax in structs)
                {
                    allTypes.Add(new TypeDefinitionInfo
                    {
                        TypeName = structSyntax.Identifier.Text,
                        TypeKind = "struct",
                        FilePath = filePath,
                        Node = structSyntax,
                        StartLine = syntaxTree.GetLineSpan(structSyntax.Span).StartLinePosition.Line + 1
                    });
                }

                // 查找接口定义（仅顶层接口）
                var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                    .Where(i => i.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax ||
                               i.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax ||
                               i.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax);
                foreach (var interfaceSyntax in interfaces)
                {
                    allTypes.Add(new TypeDefinitionInfo
                    {
                        TypeName = interfaceSyntax.Identifier.Text,
                        TypeKind = "interface",
                        FilePath = filePath,
                        Node = interfaceSyntax,
                        StartLine = syntaxTree.GetLineSpan(interfaceSyntax.Span).StartLinePosition.Line + 1
                    });
                }

                // 查找委托定义
                var delegates = root.DescendantNodes().OfType<DelegateDeclarationSyntax>();
                foreach (var delegateSyntax in delegates)
                {
                    allTypes.Add(new TypeDefinitionInfo
                    {
                        TypeName = delegateSyntax.Identifier.Text,
                        TypeKind = "delegate",
                        FilePath = filePath,
                        Node = delegateSyntax,
                        StartLine = syntaxTree.GetLineSpan(delegateSyntax.Span).StartLinePosition.Line + 1
                    });
                }
            }

            return allTypes;
        }

        /// <summary>
        /// 查找重复的类型定义
        /// </summary>
        private Dictionary<string, List<TypeDefinitionInfo>> FindDuplicates(
            List<TypeDefinitionInfo> allTypes)
        {
            var duplicates = new Dictionary<string, List<TypeDefinitionInfo>>();

            var grouped = allTypes.GroupBy(t => t.TypeName);
            foreach (var group in grouped)
            {
                if (group.Count() > 1)
                {
                    duplicates[group.Key] = group.OrderBy(t => t.FilePath).ToList();
                }
            }

            return duplicates;
        }

        /// <summary>
        /// 注释掉类型定义
        /// </summary>
        private void CommentOutTypeDefinition(TypeDefinitionInfo typeInfo)
        {
            var filePath = typeInfo.FilePath;
            var lines = File.ReadAllLines(filePath).ToList();

            // 获取类型定义的起始和结束行
            var code = File.ReadAllText(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var span = typeInfo.Node.Span;
            var lineSpan = syntaxTree.GetLineSpan(span);

            int startLine = lineSpan.StartLinePosition.Line;
            int endLine = lineSpan.EndLinePosition.Line;

            // 检查前面是否有XML注释，如果有也需要注释掉
            int commentStartLine = startLine;
            for (int i = startLine - 1; i >= 0; i--)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("///") || trimmed.StartsWith("//"))
                {
                    commentStartLine = i;
                }
                else if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    break;
                }
            }

            // 检查前面是否有特性标记，如果有也需要注释掉
            for (int i = commentStartLine - 1; i >= 0; i--)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("[") && trimmed.Contains("]"))
                {
                    commentStartLine = i;
                }
                else if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    break;
                }
            }

            // 添加注释说明
            var indentation = GetIndentation(lines[startLine]);
            lines.Insert(commentStartLine, $"{indentation}/* ===== 以下类型定义被自动注释（重复定义） =====");
            lines.Insert(commentStartLine + 1, $"{indentation}   类型: {typeInfo.TypeKind} {typeInfo.TypeName}");
            lines.Insert(commentStartLine + 2, $"{indentation}   原因: 此类型在其他文件中已定义");
            lines.Insert(commentStartLine + 3, $"{indentation}   工具: GeneratedCodeDeduplicator");

            // 注释掉类型定义的每一行
            for (int i = commentStartLine + 4; i <= endLine + 4; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                {
                    lines[i] = "// " + lines[i];
                }
            }

            // 添加结束注释
            lines.Insert(endLine + 5, $"{indentation}   ===== 重复定义注释结束 ===== */");

            // 写回文件
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        /// <summary>
        /// 获取行的缩进
        /// </summary>
        private string GetIndentation(string line)
        {
            var match = Regex.Match(line, @"^(\s*)");
            return match.Success ? match.Groups[1].Value : "";
        }
    }

    /// <summary>
    /// 类型定义信息
    /// </summary>
    internal class TypeDefinitionInfo
    {
        public string TypeName { get; set; } = string.Empty;
        public string TypeKind { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public SyntaxNode Node { get; set; } = null!;
        public int StartLine { get; set; }
    }

    /// <summary>
    /// 去重结果
    /// </summary>
    public class DeduplicationResult
    {
        /// <summary>是否成功</summary>
        public bool Success { get; set; }

        /// <summary>发现的总类型数</summary>
        public int TotalTypesFound { get; set; }

        /// <summary>发现的重复类型数</summary>
        public int DuplicateTypesFound { get; set; }

        /// <summary>已注释的类型列表</summary>
        public List<string> CommentedTypes { get; set; } = new();

        /// <summary>错误信息</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>获取摘要</summary>
        public string GetSummary()
        {
            if (!Success)
            {
                return $"去重失败: {ErrorMessage}";
            }

            if (DuplicateTypesFound == 0)
            {
                return $"检查完成：共 {TotalTypesFound} 个类型，无重复";
            }

            return $"去重完成：共 {TotalTypesFound} 个类型，{DuplicateTypesFound} 个重复，已注释 {CommentedTypes.Count} 个";
        }
    }
}

