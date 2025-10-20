using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tecan.Sila2;
using Tecan.Sila2.Client;
using Tecan.Sila2.Client.ExecutionManagement;
using Tecan.Sila2.DynamicClient;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 服务器交互服务 - 处理与 SiLA2 服务器的实际交互
    /// </summary>
    public class ServerInteractionService
    {
        private readonly Dictionary<string, CancellationTokenSource> _subscriptions = new();
        private readonly IExecutionManagerFactory _executionManagerFactory;

        public ServerInteractionService()
        {
            _executionManagerFactory = new ExecutionManagerFactory(Array.Empty<IClientRequestInterceptor>());
        }

        /// <summary>
        /// 创建 Feature 上下文
        /// </summary>
        private FeatureContext CreateFeatureContext(ServerData serverData, Feature feature)
        {
            var executionManager = _executionManagerFactory.CreateExecutionManager(serverData);
            return new FeatureContext(feature, serverData, executionManager);
        }

        /// <summary>
        /// 获取属性值（一次性）
        /// </summary>
        public async Task<string> GetPropertyValueAsync(
            ServerData serverData,
            Feature feature,
            FeatureProperty property)
        {
            try
            {
                var context = CreateFeatureContext(serverData, feature);
                var client = new PropertyClient(property, context);

                var result = await Task.Run(() => client.RequestValue());
                
                return FormatResult(result);
            }
            catch (Exception ex)
            {
                return $"❌ 错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 订阅属性（持续接收更新）
        /// </summary>
        public async Task SubscribePropertyAsync(
            ServerData serverData,
            Feature feature,
            FeatureProperty property,
            Action<string> onUpdate,
            string subscriptionId)
        {
            try
            {
                // 取消之前的订阅
                if (_subscriptions.TryGetValue(subscriptionId, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                var cts = new CancellationTokenSource();
                _subscriptions[subscriptionId] = cts;

                var context = CreateFeatureContext(serverData, feature);
                var client = new PropertyClient(property, context);

                void OnValueReceived(DynamicObjectProperty value)
                {
                    var formattedValue = FormatResult(value);
                    onUpdate?.Invoke(formattedValue);
                }

                await client.Subscribe(OnValueReceived, cts.Token);
            }
            catch (OperationCanceledException)
            {
                onUpdate?.Invoke("⏸️ 订阅已取消");
            }
            catch (Exception ex)
            {
                onUpdate?.Invoke($"❌ 错误: {ex.Message}");
            }
            finally
            {
                if (_subscriptions.TryGetValue(subscriptionId, out var cts))
                {
                    _subscriptions.Remove(subscriptionId);
                    cts.Dispose();
                }
            }
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public void UnsubscribeProperty(string subscriptionId)
        {
            if (_subscriptions.TryGetValue(subscriptionId, out var cts))
            {
                cts.Cancel();
                _subscriptions.Remove(subscriptionId);
                cts.Dispose();
            }
        }

        /// <summary>
        /// 执行不可观察命令
        /// </summary>
        public async Task<string> ExecuteUnobservableCommandAsync(
            ServerData serverData,
            Feature feature,
            FeatureCommand command,
            Dictionary<string, object> parameters)
        {
            try
            {
                var context = CreateFeatureContext(serverData, feature);
                var client = new NonObservableCommandClient(command, context);

                var request = client.CreateRequest();
                SetRequestParameters(request, command, parameters);

                var result = await client.InvokeAsync(request);
                
                return FormatResult(result);
            }
            catch (Exception ex)
            {
                return $"❌ 错误: {ex.Message}\n{ex.StackTrace}";
            }
        }

        /// <summary>
        /// 执行可观察命令
        /// </summary>
        public async Task<string> ExecuteObservableCommandAsync(
            ServerData serverData,
            Feature feature,
            FeatureCommand command,
            Dictionary<string, object> parameters,
            Action<string> onProgress,
            string commandId)
        {
            try
            {
                var context = CreateFeatureContext(serverData, feature);
                var client = new ObservableCommandClient(command, context);

                var request = client.CreateRequest();
                SetRequestParameters(request, command, parameters);

                var cts = new CancellationTokenSource();
                _subscriptions[commandId] = cts;

                var dynamicCommand = client.Invoke(request);
                dynamicCommand.Start();

                // 监听状态更新
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var state in dynamicCommand.StateUpdates.ReadAllAsync(cts.Token))
                        {
                            var progressInfo = $"进度: {state.Progress:P0}, 状态: {state.State}, 剩余: {state.EstimatedRemainingTime.TotalSeconds:F0}s";
                            onProgress?.Invoke(progressInfo);
                        }
                    }
                    catch (OperationCanceledException) { }
                }, cts.Token);

                // 等待最终结果
                var result = await dynamicCommand.Response;
                
                _subscriptions.Remove(commandId);
                cts.Dispose();

                return FormatResult(result);
            }
            catch (OperationCanceledException)
            {
                return "⏸️ 命令已取消";
            }
            catch (Exception ex)
            {
                return $"❌ 错误: {ex.Message}\n{ex.StackTrace}";
            }
        }

        /// <summary>
        /// 取消可观察命令
        /// </summary>
        public void CancelCommand(string commandId)
        {
            if (_subscriptions.TryGetValue(commandId, out var cts))
            {
                cts.Cancel();
                _subscriptions.Remove(commandId);
                cts.Dispose();
            }
        }

        /// <summary>
        /// 设置请求参数
        /// </summary>
        private void SetRequestParameters(
            DynamicRequest request,
            FeatureCommand command,
            Dictionary<string, object> parameters)
        {
            if (command.Parameter == null || !command.Parameter.Any())
                return;

            var dynamicObject = new DynamicObject();

            foreach (var param in command.Parameter)
            {
                if (parameters.TryGetValue(param.Identifier, out var value) && value != null)
                {
                    var property = new DynamicObjectProperty(param)
                    {
                        Value = ConvertValue(value, param.DataType)
                    };
                    dynamicObject.Elements.Add(property);
                }
            }

            request.Value = dynamicObject;
        }

        /// <summary>
        /// 转换参数值到正确的类型
        /// </summary>
        private object? ConvertValue(object? value, DataTypeType dataType)
        {
            if (value == null) return null;

            var basicType = GetBasicType(dataType);
            
            try
            {
                switch (basicType)
                {
                    case BasicType.Integer:
                        return Convert.ToInt64(value);
                    case BasicType.Real:
                        return Convert.ToDouble(value);
                    case BasicType.Boolean:
                        return Convert.ToBoolean(value);
                    case BasicType.String:
                        return value?.ToString() ?? string.Empty;
                    default:
                        return value;
                }
            }
            catch
            {
                return value;
            }
        }

        private BasicType? GetBasicType(DataTypeType dataType)
        {
            if (dataType?.Item is BasicType basicType)
                return basicType;
            if (dataType?.Item is ConstrainedType constrainedType)
                return GetBasicType(constrainedType.DataType);
            return null;
        }

        /// <summary>
        /// 格式化结果为 JSON 字符串
        /// </summary>
        private string FormatResult(DynamicObjectProperty result)
        {
            if (result == null)
                return "null";

            try
            {
                var resultObject = ConvertToSerializable(result);
                var json = JsonSerializer.Serialize(resultObject, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                return json;
            }
            catch (Exception ex)
            {
                return $"格式化错误: {ex.Message}\n原始值: {result}";
            }
        }

        /// <summary>
        /// 将 DynamicObjectProperty 转换为可序列化的对象
        /// </summary>
        private object? ConvertToSerializable(DynamicObjectProperty? property)
        {
            if (property?.Value == null)
                return null;

            if (property.Value is DynamicObject dynamicObj)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var element in dynamicObj.Elements)
                {
                    dict[element.Identifier ?? element.DisplayName] = ConvertToSerializable(element);
                }
                return dict;
            }

            if (property.Value is IEnumerable<object> enumerable && !(property.Value is string))
            {
                return enumerable.Select(item => 
                    item is DynamicObjectProperty prop ? ConvertToSerializable(prop) : item
                ).ToList();
            }

            return property.Value;
        }

        /// <summary>
        /// 清理所有订阅
        /// </summary>
        public void Dispose()
        {
            foreach (var cts in _subscriptions.Values)
            {
                cts?.Cancel();
                cts?.Dispose();
            }
            _subscriptions.Clear();
        }
    }
}

