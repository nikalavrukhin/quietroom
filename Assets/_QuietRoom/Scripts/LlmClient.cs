using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace QuietRoom
{
    // Запросы к нейросети (формат OpenAI chat/completions): основной сервис — DeepSeek напрямую (работает из РФ без VPN),
    // запасной — OpenRouter (из РФ только с VPN). Ключи лежат в llm.json рядом с папкой Assets — в сборку и в git он не попадает.
    public static class LlmClient
    {
        [Serializable] public class Msg { public string role; public string content; }
        [Serializable] class Endpoint { public string apiBase; public string apiKey; public string model; }
        [Serializable] class Config { public string apiBase; public string apiKey; public string model; public Endpoint fallback; }
        [Serializable] class Format { public string type = "json_object"; }
        // Без скрытых размышлений: ответ за 1,5–4 с вместо 5–30 (проверяли на промпте Белых — логика дела держится так же).
        // thinking — выключатель DeepSeek, reasoning и provider — OpenRouter (самый быстрый провайдер); чужие поля оба сервиса пропускают.
        [Serializable] class Thinking { public string type = "disabled"; }
        [Serializable] class Reasoning { public bool enabled; }
        [Serializable] class Provider { public string sort = "latency"; }
        [Serializable] class Request { public string model; public Msg[] messages; public float temperature; public int max_tokens; public Format response_format;
            public Thinking thinking = new Thinking(); public Reasoning reasoning = new Reasoning(); public Provider provider = new Provider(); }
        [Serializable] class Choice { public Msg message; }
        [Serializable] class Error { public string message; }
        [Serializable] class Response { public Choice[] choices; public Error error; }

        static Config config;
        // Строгий JSON-режим. Если провайдер его не умеет (ошибка 400), выключаем и повторяем без него.
        static bool jsonMode = true;
        // Один следующий запрос — без строгого JSON (переспрос после мусорного ответа).
        public static bool plainOnce;

        static Config Load()
        {
            if (config != null) return config;
            string[] paths =
            {
                Path.Combine(Application.dataPath, "..", "llm.json"),
                // В собранной игре dataPath = Подвал.app/Contents, ключ лежит рядом с .app.
                Path.Combine(Application.dataPath, "..", "..", "llm.json"),
                Path.Combine(Application.persistentDataPath, "llm.json"),
            };
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
                config = JsonUtility.FromJson<Config>(File.ReadAllText(p));
                return config;
            }
            return null;
        }

        public static IEnumerator Chat(List<Msg> messages, Action<string> onReply, Action<string> onError)
        {
            var cfg = Load();
            if (cfg == null || string.IsNullOrEmpty(cfg.apiKey))
            {
                onError("нет файла llm.json с ключом нейросети");
                yield break;
            }

            bool useJson = jsonMode && !plainOnce;
            plainOnce = false;

            // Порядок попыток: основной сервис дважды, потом запасной (если он есть в llm.json).
            // Таймаут 40 с; ошибку игроку показываем, только когда не помогло ничего.
            var plan = new List<Endpoint> { new Endpoint { apiBase = cfg.apiBase, apiKey = cfg.apiKey, model = cfg.model } };
            plan.Add(plan[0]);
            if (cfg.fallback != null && !string.IsNullOrEmpty(cfg.fallback.apiKey)) plan.Add(cfg.fallback);
            else plan.Add(plan[0]);

            for (int attempt = 0; attempt < plan.Count; attempt++)
            {
                var ep = plan[attempt];
                bool last = attempt == plan.Count - 1;
                var request = new Request
                {
                    model = ep.model,
                    messages = messages.ToArray(),
                    temperature = 0.85f,
                    max_tokens = 600,
                    response_format = new Format(),
                };
                var body = JsonUtility.ToJson(request);
                // JsonUtility пишет пустой объект вместо null — убираем поле целиком.
                if (!useJson) body = body.Replace(",\"response_format\":{\"type\":\"json_object\"}", "");

                using (var req = new UnityWebRequest(ep.apiBase.TrimEnd('/') + "/chat/completions", "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", "Bearer " + ep.apiKey);
                    req.SetRequestHeader("X-Title", "QuietRoom");
                    req.timeout = 40;
                    yield return req.SendWebRequest();

                    string text = req.downloadHandler != null ? req.downloadHandler.text : "";
                    Response resp = null;
                    try { resp = JsonUtility.FromJson<Response>(text); } catch { }

                    if (req.result != UnityWebRequest.Result.Success && useJson && req.responseCode == 400)
                    {
                        jsonMode = false;
                        Debug.LogWarning("[QuietRoom] JSON-режим не поддержан, повторяю без него");
                        yield return Chat(messages, onReply, onError);
                        yield break;
                    }
                    bool ok = req.result == UnityWebRequest.Result.Success
                        && resp != null && resp.choices != null && resp.choices.Length > 0 && resp.choices[0].message != null;
                    if (ok)
                    {
                        onReply(resp.choices[0].message.content ?? "");
                        yield break;
                    }
                    string why = resp != null && resp.error != null && !string.IsNullOrEmpty(resp.error.message)
                        ? resp.error.message : (req.result == UnityWebRequest.Result.Success ? "пустой ответ" : req.error);
                    Debug.LogWarning("[QuietRoom] LLM " + ep.apiBase + ": попытка " + (attempt + 1) + " не удалась (" + req.responseCode + " " + why + ")");
                    if (last)
                    {
                        onError(why);
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
    }
}
