using UnityEngine;

namespace QuietRoom
{
    public enum Emotion { Calm, Nervous, Scared, Angry, Broken, Pain, Weak, Smug }

    // Подозреваемый: скрытое тело (здоровье, сахар, стресс) и вся живая анимация модельки.
    // Полосок на экране нет — состояние читается по позе, дыханию и дрожи.
    public class Suspect : MonoBehaviour, IInteractable
    {
        [Header("Кости модельки")]
        public Transform spine;
        public Transform torso;
        public Transform head;
        public Transform browL, browR;
        public Transform eyeL, eyeR;
        public Transform mouth;

        [Header("Тело (скрыто от игрока)")]
        [Range(0, 100)] public float health = 100f;
        [Range(0, 100)] public float sugar = 55f;
        [Range(0, 100)] public float stress = 60f;   // его только что похитили
        public Emotion emotion = Emotion.Scared;
        public bool dead;

        [Header("Что с ним делают")]
        public bool lampInFace;
        public float gunPointedUntil;
        public bool talking;
        public int hits;

        // Скорость обмена: сахар в секунду реального времени.
        const float SugarDrain = 0.03f;   // под партию в 30 минут: без шоколадки к середине ему плохо

        Transform readTarget;  // что сейчас читает на столе
        float readUntil;
        float aimTime, notAimedTime;
        bool threatened;       // уже среагировал на наведённый ствол

        Vector3 kick;          // отдача головы от удара, градусы
        float spineKick;
        float lastHit = -10f;
        float deathT;
        Vector3 torsoBase = Vector3.one;
        bool baseSaved;

        public bool GunPointed => Time.time < gunPointedUntil;

        GameDirector Game => GameDirector.I;
        Interrogation talk;
        Interrogation Talk => talk != null ? talk : (talk = FindAnyObjectByType<Interrogation>());

        void Update()
        {
            if (!baseSaved && torso != null) { torsoBase = torso.localScale; baseSaved = true; }
            if (Game != null && Game.Playing && !dead) Simulate(Time.deltaTime);
            Animate(Time.deltaTime);
            UpdateVoice();
        }

        float nextCreak = 6f;

        void Simulate(float dt)
        {
            // Ёрзает на стуле — чаще, когда нервничает.
            if (Time.time > nextCreak)
            {
                Sfx.Play("chair_creak", transform.position + Vector3.up * 0.45f, 0.25f + stress / 400f);
                nextCreak = Time.time + Random.Range(6f, 16f) * (1.4f - stress / 100f);
            }
            sugar -= SugarDrain * (1f + stress / 80f) * dt;
            if (sugar < 20f) health -= (sugar < 8f ? 0.16f : 0.05f) * dt;
            if (sugar < 0f) sugar = 0f;

            float calmTarget = 40f;   // совсем он не успокоится — он похищен
            if (lampInFace) stress += 0.25f * dt;
            if (GunPointed) stress += 1.2f * dt;
            stress = Mathf.MoveTowards(stress, calmTarget, 0.04f * dt);
            // Сердце: долгая паника на пределе потихоньку бьёт по здоровью.
            if (stress > 90f) health -= 0.03f * dt;
            stress = Mathf.Clamp(stress, 0f, 100f);

            if (health <= 0f) Die(sugar < 8f
                ? "Сахар кончился. Белых перестал дрожать, потом перестал дышать.\nВ портфеле так и лежала шоколадка."
                : "Сердце Белых не выдержало.\nКод ячейки №212 больше никто не назовёт.");
        }

        // ---------- действия детектива ----------

        public string Hint(PlayerController player)
        {
            if (dead) return "";
            if (player.held == null)
                return player.CanHit(this) ? "Enter — говорить · " + Binds.Label(Act.Hit) + " — ударить" : "Enter — говорить";
            switch (player.held.kind)
            {
                case ItemKind.Water:
                    var w = player.held.GetComponent<GlassWater>();
                    return w != null && w.Empty ? "стакан пуст" : Binds.Label(Act.Use) + " — дать попить";
                case ItemKind.Chocolate:
                    var cb = player.held.GetComponent<ChocolateBar>();
                    if (cb != null && !cb.unwrapped) return Binds.Label(Act.Use) + " — развернуть шоколадку";
                    return cb != null && cb.Left > 0 ? Binds.Label(Act.Use) + (cb.Left == 2 ? " — дать откусить" : " — дать доесть") : "";
                case ItemKind.Pistol: return Binds.Label(Act.Use) + " — приставить ствол ко лбу · ЛКМ — выстрелить";
                default: return Binds.Label(Act.Use) + " — положить перед ним: " + player.held.title;
            }
        }

        public void Interact(PlayerController player)
        {
            if (dead || player.held == null) return;
            var item = player.held;
            // Вода — глотками, пока не кончится.
            if (item.kind == ItemKind.Water)
            {
                var water = item.GetComponent<GlassWater>();
                if (water == null || water.Empty || item.gestureUntil > Time.time) return;
                water.Sip();
                item.Gesture(new Vector3(55f, 0f, 0f), new Vector3(-0.08f, 0.08f, 0.15f), 1.4f);   // наклон от себя — к его губам
                health = Mathf.Min(100f, health + 4f);
                stress = Mathf.Max(0f, stress - 7f);
                Talk.PlayerAction(water.fill - water.sip < 0.04f
                    ? "Детектив дал тебе допить воду из стакана — до дна."
                    : "Детектив поднёс к твоим губам стакан, ты сделал несколько глотков воды.", true);
                return;
            }
            // Шоколад — в два укуса: уже первого хватает, чтобы стало явно легче.
            if (item.kind == ItemKind.Chocolate) { Feed(player, item); return; }
            // Бумаги, телефон и фото кладутся на стол перед ним — пусть читает сам.
            if (item.kind == ItemKind.Folder || item.kind == ItemKind.SuspectPhone || item.kind == ItemKind.Photo)
            {
                if (PresentSpot.I != null) PresentSpot.I.Place(player);
                return;
            }

            bool again = item.used;
            switch (item.kind)
            {
                case ItemKind.Chocolate: sugar = Mathf.Min(100f, sugar + 45f); stress -= 5f; break;
                case ItemKind.Folder: stress += again ? 4f : 18f; break;
                case ItemKind.SuspectPhone: stress += again ? 4f : 22f; break;
                case ItemKind.Photo: stress += again ? 2f : 8f; break;
                case ItemKind.Pistol: stress += 30f; gunPointedUntil = Time.time + 8f; threatened = true; Flinch(); break;
            }
            stress = Mathf.Clamp(stress, 0f, 100f);
            if (!item.Consumable) item.used = true;

            if (item.kind == ItemKind.SuspectPhone) Talk.sawThreat = true;
            Talk.PlayerAction(CaseFile.UseEvent(item.kind, again), true);
            if (item.Consumable && item.Consume()) player.held = null;
        }

        // ---------- шоколад: два укуса ----------

        int chocoEaten;          // сколько раз он откусил за партию
        float chewUntil;

        void Feed(PlayerController player, Pickup item)
        {
            var bar = item.GetComponent<ChocolateBar>();
            if (bar == null) return;
            if (!bar.unwrapped) { bar.Unwrap(); return; }
            if (bar.busy || bar.Left <= 0 || item.gestureUntil > Time.time) return;

            bar.EatPiece(0.45f);
            item.Gesture(new Vector3(14f, 0f, 0f), new Vector3(-0.1f, 0.06f, 0.2f), 1.1f);   // поднести ко рту
            chocoEaten++;
            chewUntil = Time.time + 2.2f;
            bool leftNow = bar.Left - 1 <= 0;

            // Первый укус — полплитки: сахар заметно подскакивает. Второй доедает остальное.
            string text;
            if (chocoEaten == 1) { sugar = Mathf.Min(100f, sugar + 40f); stress = Mathf.Max(0f, stress - 10f); health = Mathf.Min(100f, health + 3f); text = "Детектив поднёс к твоим губам твою же шоколадку «Алёнка», и ты откусил сразу половину плитки. Через минуту отпускает: дрожь уходит, голова проясняется, тебе явно лучше."; }
            else { sugar = Mathf.Min(100f, sugar + 10f); stress = Mathf.Max(0f, stress - 3f); text = "Он дал тебе доесть шоколадку. Ты съел всю плитку, тебе хорошо и сытно."; }
            stress = Mathf.Clamp(stress, 0f, 100f);
            Talk.PlayerAction(text, true);

            if (leftNow) StartCoroutine(FinishBar(player, item));
        }

        System.Collections.IEnumerator FinishBar(PlayerController player, Pickup item)
        {
            yield return new WaitForSeconds(1.4f);
            if (player.held == item) player.held = null;
            item.gameObject.SetActive(false);
        }

        // Детектив положил предмет на стол перед ним.
        public void Present(Pickup item, bool again)
        {
            if (dead) return;
            readTarget = item.transform;
            readUntil = Time.time + (again ? 3f : 6f);
            switch (item.kind)
            {
                case ItemKind.Folder: stress += again ? 4f : 18f; break;
                case ItemKind.SuspectPhone: stress += again ? 4f : 22f; break;
                case ItemKind.Photo: stress += again ? 2f : 8f; break;
                case ItemKind.Pistol: stress += 15f; break;
            }
            stress = Mathf.Clamp(stress, 0f, 100f);
            if (item.kind == ItemKind.SuspectPhone) Talk.sawThreat = true;
            Talk.PlayerAction(CaseFile.PlaceEvent(item.kind, again), true);
        }

        // Каждый кадр от игрока: смотрит ли на него ствол.
        public void UpdateAim(bool aimed, float dt)
        {
            if (dead) return;
            if (aimed)
            {
                aimTime += dt;
                notAimedTime = 0f;
                gunPointedUntil = Mathf.Max(gunPointedUntil, Time.time + 0.4f);
                if (!threatened && aimTime > 0.2f)
                {
                    threatened = true;
                    stress = Mathf.Min(100f, stress + 25f);
                    emotion = Emotion.Scared;
                    Flinch();
                    Talk.PlayerAction("Детектив молча навёл на тебя пистолет. Ствол смотрит тебе в лицо.", true);
                }
            }
            else
            {
                aimTime = 0f;
                notAimedTime += dt;
                if (threatened && notAimedTime > 3f)
                {
                    threatened = false;
                    Talk.PlayerAction("Детектив опустил пистолет.", false);
                }
            }
        }

        void Flinch()
        {
            kick = new Vector3(-12f, (Random.value < 0.5f ? -1f : 1f) * 28f, 0f);
            spineKick = -12f;
        }

        public void Hit()
        {
            if (dead || Time.time - lastHit < 0.9f) return;
            lastHit = Time.time;
            hits++;
            health -= 13f + (sugar < 20f ? 6f : 0f) + (hits > 4 ? 5f : 0f);
            stress = Mathf.Min(100f, stress + 22f);
            kick = new Vector3(Random.Range(-8f, 4f), (Random.value < 0.5f ? -1f : 1f) * 32f, Random.Range(-12f, 12f));
            spineKick = 9f;
            emotion = Emotion.Pain;
            Sfx.Play("punch", head.position, 0.9f);
            if (Random.value < 0.6f) Sfx.Play("chair_creak", transform.position + Vector3.up * 0.45f, 0.5f);
            if (health <= 0f) { Die("Ты бил, пока он не перестал отвечать.\nБелых мёртв. Код ячейки №212 умер вместе с ним."); return; }
            Talk.PlayerAction("Детектив ударил тебя по лицу" + (hits > 1 ? " (уже " + hits + "-й раз)." : "."), true);
        }

        public void Shot()
        {
            if (dead) return;
            Sfx.Play("gunshot", head.position, 1f, 0.03f, 0.3f);
            kick = new Vector3(-25f, 10f, 0f);
            spineKick = -14f;
            health = 0f;
            Die("Выстрел в бетонной коробке звучит как конец света.\nБелых мёртв. Код ячейки №212 — вместе с ним.");
        }

        void Die(string text)
        {
            if (dead) return;
            dead = true;
            health = 0f;
            talking = false;
            if (Game != null) Game.Lose(GameState.LostDead, "ОН МЁРТВ", text);
        }

        // ---------- голос ----------
        // Глухое бормотание под нос: мычание (hum_1…hum_5) ниже на треть, приглушённое фильтром,
        // громкость пульсирует в ритме слогов и стихает на знаках препинания.

        AudioSource voice;
        AudioLowPassFilter muffle;
        float voiceLevel, syllablePhase;

        void UpdateVoice()
        {
            if (head == null) return;
            if (voice == null)
            {
                voice = head.gameObject.AddComponent<AudioSource>();
                voice.spatialBlend = 0.7f;
                voice.minDistance = 1.2f;
                voice.maxDistance = 10f;
                voice.playOnAwake = false;
                voice.loop = true;
                voice.volume = 0f;
                muffle = head.gameObject.AddComponent<AudioLowPassFilter>();
                muffle.cutoffFrequency = 900f;
            }
            bool weak = emotion == Emotion.Weak || health < 35f;
            float target = 0f;
            if (talking && !dead)
            {
                if (!voice.isPlaying)
                {
                    voice.clip = Sfx.Get("hum_" + Random.Range(1, 6));
                    if (voice.clip != null)
                    {
                        voice.time = Random.Range(0f, voice.clip.length * 0.5f);
                        voice.Play();
                    }
                }
                voice.pitch = (weak ? 0.58f : 0.66f) + (emotion == Emotion.Scared ? 0.05f : 0f);
                // Слоги: около пяти в секунду, у каждого своя сила; на знаках препинания — пауза.
                syllablePhase += Time.deltaTime * (emotion == Emotion.Scared ? 6.5f : 5f);
                float syl = Mathf.Pow(Mathf.Abs(Mathf.Sin(syllablePhase * Mathf.PI)), 0.6f)
                          * (0.55f + 0.45f * Mathf.PerlinNoise(syllablePhase * 0.7f, 3.1f));
                char c = CurrentChar();
                bool pause = c == '.' || c == ',' || c == '!' || c == '?' || c == '…' || c == '—';
                target = pause ? 0f : syl * (weak ? 0.35f : 0.6f);
                muffle.cutoffFrequency = weak ? 650f : 900f;
            }
            voiceLevel = Mathf.MoveTowards(voiceLevel, target, Time.deltaTime * 25f);
            voice.volume = voiceLevel;
            if (!talking && voiceLevel <= 0.001f && voice.isPlaying) voice.Stop();
        }

        // Какая буква сейчас печатается в его реплике — чтобы голос замолкал на запятых и точках.
        char CurrentChar()
        {
            var line = Talk != null ? Talk.Current : null;
            if (line == null || line.text.Length == 0) return ' ';
            int i = Mathf.Clamp((int)line.shown - 1, 0, line.text.Length - 1);
            return line.text[i];
        }

        // ---------- описание тела для ИИ ----------

        public string BodyForAI()
        {
            var sb = new System.Text.StringBuilder();
            if (health > 80f) sb.Append("Тело: цел, только затекли руки. ");
            else if (health > 55f) sb.Append("Тело: лицо горит после ударов, разбита губа. ");
            else if (health > 30f) sb.Append("Тело: сильно избит, кровь во рту, тяжело говорить. ");
            else sb.Append("Тело: почти без сознания, каждое слово даётся с трудом, сердце колотится с перебоями. ");

            if (sugar > 40f) { }
            else if (sugar > 20f) sb.Append("Сахар падает: подташнивает, лёгкая дрожь в руках. ");
            else if (sugar > 8f) sb.Append("Гипогликемия: сильная дрожь, холодный пот, путаешь слова и мысли. ");
            else sb.Append("Гипогликемия на грани обморока: язык не слушается, перед глазами плывёт. ");

            if (stress > 80f) sb.Append("Ты в панике. ");
            else if (stress > 55f) sb.Append("Ты сильно нервничаешь. ");
            else if (stress < 25f) sb.Append("Ты почти успокоился. ");

            if (lampInFace) sb.Append("Лампа бьёт прямо в глаза, детектива почти не видно. ");
            if (GunPointed) sb.Append("На тебя направлен пистолет. ");
            return sb.ToString().Trim();
        }

        // ---------- анимация ----------

        void Animate(float dt)
        {
            if (spine == null || head == null) return;
            float t = Time.time;
            kick = Vector3.Lerp(kick, Vector3.zero, dt * 4f);
            spineKick = Mathf.Lerp(spineKick, 0f, dt * 3f);

            if (dead)
            {
                deathT = Mathf.Min(1f, deathT + dt * 0.8f);
                float e = 1f - Mathf.Pow(1f - deathT, 3f);
                spine.localRotation = Quaternion.Euler(22f * e + spineKick, 0f, 6f * e);
                head.localRotation = Quaternion.Euler(Vector3.Lerp(Vector3.zero, new Vector3(55f, 18f, 14f), e) + kick);
                if (torso != null) torso.localScale = torsoBase;
                SetMouth(1.8f, 0f);
                SetBrows(0f);
                SetEyes(0.15f);
                return;
            }

            // Дыхание: чем хуже, тем чаще и глубже.
            float rate = 1.1f + stress / 45f + (health < 40f ? 1.2f : 0f);
            float depth = 0.012f + (health < 40f ? 0.02f : 0f) + stress / 5000f;
            if (torso != null)
                torso.localScale = new Vector3(torsoBase.x * (1f + Mathf.Sin(t * rate) * depth * 0.6f),
                    torsoBase.y * (1f + Mathf.Sin(t * rate) * depth), torsoBase.z * (1f + Mathf.Sin(t * rate) * depth));

            // Дрожь от страха и нехватки сахара.
            float tremble = (GunPointed ? 0.5f : 0f) + Mathf.Clamp01((stress - 55f) / 45f) * 0.6f + Mathf.Clamp01((28f - sugar) / 28f) * 1.2f;
            Vector3 shake = tremble * new Vector3(Mathf.PerlinNoise(t * 9f, 1f) - 0.5f, Mathf.PerlinNoise(1f, t * 9f) - 0.5f, Mathf.PerlinNoise(t * 7f, t * 3f) - 0.5f) * 5f;

            // Куда смотрит: на детектива, с поправкой на эмоцию.
            float yaw = 0f, pitch = 0f, roll = 0f, lean = 0f;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = spine.InverseTransformDirection(cam.transform.position - head.position);
                yaw = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -40f, 40f);
                pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
            }
            bool reading = readTarget != null && Time.time < readUntil && !GunPointed;
            if (reading)
            {
                // Опускает глаза на бумаги и чуть водит головой, будто читает строчки.
                Vector3 dir = spine.InverseTransformDirection(readTarget.position - head.position);
                yaw = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -40f, 40f) + Mathf.Sin(t * 1.7f) * 6f;
                pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -10f, 50f);
            }
            float weak = Mathf.Clamp01((45f - health) / 45f) + Mathf.Clamp01((12f - sugar) / 12f) * 0.6f;
            if (!reading)
            switch (emotion)
            {
                case Emotion.Scared: yaw += 22f * Mathf.Sin(t * 0.7f); pitch += 8f; lean = -6f; break;
                case Emotion.Angry: lean = 7f; pitch -= 3f; break;
                case Emotion.Broken: pitch += 22f; lean = 6f; yaw *= 0.3f; break;
                case Emotion.Pain: pitch += 12f; roll = 8f; lean = 4f; break;
                case Emotion.Weak: pitch += 28f; roll = 10f; lean = 10f; break;
                case Emotion.Smug: pitch -= 6f; roll = -7f; lean = -4f; break;
                case Emotion.Nervous: yaw += 10f * Mathf.Sin(t * 1.3f); break;
            }
            if (lampInFace) { pitch += 10f; yaw += 12f; }
            if (GunPointed && emotion != Emotion.Angry)
            {
                // Вжимается в спинку и косится в сторону, не сводя глаз со ствола надолго.
                lean -= 8f;
                yaw += 18f * Mathf.Sin(t * 0.9f);
                pitch += 6f;
            }
            pitch += weak * 30f;
            roll += weak * 9f;
            lean += weak * 12f;

            var headTarget = Quaternion.Euler(new Vector3(pitch, yaw, roll) + kick + shake);
            head.localRotation = Quaternion.Slerp(head.localRotation, headTarget, dt * 5f);
            var spineTarget = Quaternion.Euler(lean + spineKick + shake.x * 0.3f, 0f, shake.z * 0.2f);
            spine.localRotation = Quaternion.Slerp(spine.localRotation, spineTarget, dt * 3f);

            // Лицо.
            float browAngle = 0f;
            switch (emotion)
            {
                case Emotion.Angry: browAngle = -18f; break;
                case Emotion.Smug: browAngle = -6f; break;
                case Emotion.Scared: case Emotion.Broken: case Emotion.Nervous: browAngle = 16f; break;
                case Emotion.Pain: browAngle = 22f; break;
            }
            SetBrows(browAngle);
            SetEyes(lampInFace ? 0.25f : emotion == Emotion.Scared || GunPointed ? 1.35f : emotion == Emotion.Pain ? 0.35f : weak > 0.6f ? 0.5f : 1f);
            bool chewing = Time.time < chewUntil && !talking;
            float open = talking ? 1.5f + Mathf.Abs(Mathf.Sin(t * 17f)) * 3.2f : chewing ? 1.3f + Mathf.Abs(Mathf.Sin(t * 8f)) * 2.2f : emotion == Emotion.Pain ? 2.6f : 1f;
            SetMouth(open, emotion == Emotion.Smug ? 9f : 0f);
        }

        void SetBrows(float angle)
        {
            if (browL != null) browL.localRotation = Quaternion.Slerp(browL.localRotation, Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 8f);
            if (browR != null) browR.localRotation = Quaternion.Slerp(browR.localRotation, Quaternion.Euler(0f, 0f, -angle), Time.deltaTime * 8f);
        }

        void SetEyes(float open)
        {
            foreach (var eye in new[] { eyeL, eyeR })
            {
                if (eye == null) continue;
                // Глаз — пивот со склерой и зрачком: сжимаем по высоте. Иногда моргает.
                var s = eye.localScale;
                float blink = Mathf.Repeat(Time.time, 4.3f) < 0.12f ? 0.1f : 1f;
                eye.localScale = new Vector3(1f, Mathf.Lerp(s.y, Mathf.Min(1.2f, open) * blink, Time.deltaTime * 14f), 1f);
            }
        }

        void SetMouth(float open, float tilt)
        {
            if (mouth == null) return;
            var s = mouth.localScale;
            mouth.localScale = new Vector3(s.x, Mathf.Lerp(s.y, 0.008f * open, Time.deltaTime * 18f), s.z);
            mouth.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }
    }
}
