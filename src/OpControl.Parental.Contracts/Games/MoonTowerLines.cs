// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts.Games;

public enum LunaLineEvent
{
    GoodMove = 0,
    CloseCall = 1,
    Win = 2,
    Loss = 3,
    InvalidMove = 4,
    Idle = 5,
    LevelComplete = 6,
    PowerUsed = 7,
    BagEmpty = 8,
    YourTurn = 9
}

/// <summary>
/// Ready-made Luna commentary. No AI call is made for these lines; they are picked
/// at random per event and a picker avoids repeating the last few.
/// </summary>
public static class MoonTowerLunaLines
{
    public const string English = "en";
    public const string Spanish = "es-MX";

    public static string For(LunaLineEvent kind, string? language, int pick, int level = 0, int needed = 0)
    {
        var lines = Set(kind, language);
        var text = lines[Math.Abs(pick) % lines.Count];
        return string.Format(text, level, needed);
    }

    public static int Count(LunaLineEvent kind) => Set(kind, English).Count;

    public static IReadOnlyList<string> Set(LunaLineEvent kind, string? language)
    {
        var spanish = !string.IsNullOrWhiteSpace(language)
            && language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return spanish ? SpanishLines(kind) : EnglishLines(kind);
    }

    private static string[] EnglishLines(LunaLineEvent kind) => kind switch
    {
        LunaLineEvent.GoodMove =>
        [
            "Ooh, nice one!",
            "That was a smart move.",
            "You're building fast!",
            "Ooh, I like that. Well played!",
            "That fits perfectly. Nice!",
            "You saw that gap, didn't you?",
            "Great pick. I need to think now.",
            "That block was made for that spot.",
            "Clever. Very clever.",
            "You're making this tower look easy."
        ],
        LunaLineEvent.CloseCall =>
        [
            "Ooh, that was close!",
            "Just one short of making {0}!",
            "So close. Level {0} needs {1} more.",
            "You almost had it! {1} more to go.",
            "Nearly a perfect row!",
            "One more block and level {0} is yours.",
            "That gap is teasing you, isn't it?",
            "Almost, almost!",
            "So near a +10. Keep going!",
            "Careful, level {0} is watching you."
        ],
        LunaLineEvent.Win =>
        [
            "You win! That tower is all yours.",
            "Wow, you beat me! Great building.",
            "You did it! The flag is yours.",
            "Nice win! You planned that well.",
            "You win this time. I'll be back!",
            "That was a great game. You win!",
            "The tower is complete and you're on top!",
            "You win! My moon hat is off to you.",
            "Well played. You earned that flag.",
            "You win! Want to try Make 20 next?"
        ],
        LunaLineEvent.Loss =>
        [
            "I win this one, but you played well!",
            "My tower is taller today. Rematch?",
            "Good game! You nearly caught me.",
            "I win, but that was a close game.",
            "The flag is mine this time. Nice work!",
            "I got lucky. Want to play again?",
            "Good game! You had some clever moves.",
            "I win, but you made me think hard.",
            "That was fun. Shall we try again?",
            "My turn to celebrate. Great game!"
        ],
        LunaLineEvent.InvalidMove =>
        [
            "That block won't fit there. Try another spot?",
            "Oops, it needs full support underneath.",
            "Not there! Something is in the way.",
            "That spot is a little too small. Try again?",
            "Hmm, that one would hang over the edge.",
            "It has to rest fully on the blocks below.",
            "That space is taken. Pick another!",
            "Almost! Choose a spot with support.",
            "That block can't reach. Find a safer spot.",
            "Try a glowing spot, those always fit."
        ],
        LunaLineEvent.Idle =>
        [
            "Take your time. I'm not going anywhere.",
            "Thinking? Me too.",
            "The tower is waiting for you.",
            "Where will your next block go?",
            "Take a breath. You've got this.",
            "I wonder what you'll build next.",
            "No rush. Good towers take time.",
            "Hmm, which block feels right?",
            "I'm watching the flag up there.",
            "Your move, master builder."
        ],
        LunaLineEvent.LevelComplete =>
        [
            "Level {0} is complete! +10 for you!",
            "Perfect row! Level {0} adds up!",
            "That's a full level! Beautiful!",
            "Level {0} done! The tower is growing!",
            "Exactly {1}! Level {0} is finished!",
            "A perfect ten! Level {0} is yours!",
            "Level {0} complete. That sparkle is for you!",
            "Full row! Level {0} couldn't be better!",
            "Level {0} is all filled in. Nice maths!",
            "Complete! Level {0} is now part of the tower!"
        ],
        LunaLineEvent.PowerUsed =>
        [
            "Ooh, a power block!",
            "Things just got interesting!",
            "Power move! I felt that.",
            "A little moon magic there?",
            "That's a special block. Nicely used!",
            "Power blocks always surprise me!",
            "Whoosh! That changed the tower!",
            "A clever power play!",
            "I did not see that coming!",
            "Power blocks make the tower sparkle!"
        ],
        LunaLineEvent.BagEmpty =>
        [
            "The bag is empty! Every block counts now.",
            "No more blocks in the bag. Use what you have!",
            "Last blocks! Make them count.",
            "The bag is empty. This is the final stretch!",
            "No refills now. Choose carefully!",
            "Empty bag! Time for clever maths.",
            "That's the last of the blocks. Play smart!",
            "The bag is done. What's left in your hand?",
            "Final blocks! The flag is so close.",
            "Empty bag! The tower depends on you now."
        ],
        _ =>
        [
            "It's your turn!",
            "Your move, master builder.",
            "Go on, pick a block!",
            "The tower is ready for you.",
            "Your turn! What will you build?",
            "Show me your best move.",
            "I'm ready. Are you?",
            "Let's build!",
            "Your turn to shine.",
            "Go ahead, I'll wait."
        ]
    };

    private static string[] SpanishLines(LunaLineEvent kind) => kind switch
    {
        LunaLineEvent.GoodMove =>
        [
            "¡Ooh, muy bien!",
            "Ese movimiento fue inteligente.",
            "¡Estás construyendo rápido!",
            "¡Me gusta! Bien jugado.",
            "Encaja perfecto. ¡Bien!",
            "Viste ese hueco, ¿verdad?",
            "Gran elección. Ahora tengo que pensar.",
            "Ese bloque nació para ese lugar.",
            "Listo. Muy listo.",
            "Haces que esta torre parezca fácil."
        ],
        LunaLineEvent.CloseCall =>
        [
            "¡Ooh, por poco!",
            "¡Casi completas el nivel {0}!",
            "Muy cerca. Al nivel {0} le faltan {1}.",
            "¡Casi lo logras! Faltan {1}.",
            "¡Casi una fila perfecta!",
            "Un bloque más y el nivel {0} es tuyo.",
            "Ese hueco te está retando, ¿eh?",
            "¡Casi, casi!",
            "Falta poquito para el +10. ¡Sigue!",
            "Cuidado, el nivel {0} te está mirando."
        ],
        LunaLineEvent.Win =>
        [
            "¡Ganaste! La torre es toda tuya.",
            "¡Guau, me ganaste! Gran construcción.",
            "¡Lo lograste! La bandera es tuya.",
            "¡Buena victoria! Lo planeaste muy bien.",
            "Ganaste esta vez. ¡Volveré!",
            "Fue un gran juego. ¡Ganaste!",
            "¡La torre está completa y estás arriba!",
            "¡Ganaste! Me quito el sombrero lunar.",
            "Bien jugado. Te ganaste esa bandera.",
            "¡Ganaste! ¿Probamos Hacer 20?"
        ],
        LunaLineEvent.Loss =>
        [
            "¡Gano esta, pero jugaste muy bien!",
            "Mi torre es más alta hoy. ¿Revancha?",
            "¡Buen juego! Casi me alcanzas.",
            "Gano yo, pero estuvo muy reñido.",
            "La bandera es mía esta vez. ¡Bien hecho!",
            "Tuve suerte. ¿Jugamos otra vez?",
            "¡Buen juego! Tuviste movimientos ingeniosos.",
            "Gano, pero me hiciste pensar mucho.",
            "Fue divertido. ¿Intentamos de nuevo?",
            "Me toca celebrar. ¡Gran juego!"
        ],
        LunaLineEvent.InvalidMove =>
        [
            "Ese bloque no cabe ahí. ¿Otro lugar?",
            "Uy, necesita apoyo completo debajo.",
            "¡Ahí no! Hay algo en el camino.",
            "Ese lugar es muy pequeño. ¿Intentas otra vez?",
            "Mmm, ese quedaría fuera del borde.",
            "Tiene que apoyarse completo en los bloques.",
            "Ese espacio está ocupado. ¡Elige otro!",
            "¡Casi! Busca un lugar con apoyo.",
            "Ese bloque no alcanza. Prueba un lugar seguro.",
            "Prueba un lugar brillante, esos siempre caben."
        ],
        LunaLineEvent.Idle =>
        [
            "Tómate tu tiempo. Aquí sigo.",
            "¿Pensando? Yo también.",
            "La torre te está esperando.",
            "¿Dónde irá tu próximo bloque?",
            "Respira. Puedes con esto.",
            "Me pregunto qué construirás ahora.",
            "Sin prisa. Las buenas torres toman tiempo.",
            "Mmm, ¿qué bloque se siente bien?",
            "Estoy mirando la bandera de allá arriba.",
            "Tu turno, maestro constructor."
        ],
        LunaLineEvent.LevelComplete =>
        [
            "¡Nivel {0} completo! ¡+10 para ti!",
            "¡Fila perfecta! ¡El nivel {0} suma!",
            "¡Ese nivel está lleno! ¡Hermoso!",
            "¡Nivel {0} terminado! ¡La torre crece!",
            "¡Exactamente {1}! ¡Nivel {0} listo!",
            "¡Un diez perfecto! ¡El nivel {0} es tuyo!",
            "Nivel {0} completo. ¡Ese brillo es para ti!",
            "¡Fila completa! ¡El nivel {0} quedó perfecto!",
            "¡El nivel {0} está lleno! ¡Qué buena suma!",
            "¡Completo! ¡El nivel {0} ya es parte de la torre!"
        ],
        LunaLineEvent.PowerUsed =>
        [
            "¡Ooh, un bloque de poder!",
            "¡Ahora se pone interesante!",
            "¡Jugada de poder! La sentí.",
            "¿Un poco de magia lunar?",
            "Ese es un bloque especial. ¡Bien usado!",
            "¡Los bloques de poder siempre me sorprenden!",
            "¡Fiuu! ¡Eso cambió la torre!",
            "¡Una jugada de poder muy lista!",
            "¡No vi venir esa!",
            "¡Los bloques de poder hacen brillar la torre!"
        ],
        LunaLineEvent.BagEmpty =>
        [
            "¡La bolsa está vacía! Ahora cada bloque cuenta.",
            "No quedan bloques en la bolsa. ¡Usa los tuyos!",
            "¡Últimos bloques! Hazlos contar.",
            "La bolsa está vacía. ¡Recta final!",
            "Ya no hay más. ¡Elige con cuidado!",
            "¡Bolsa vacía! Hora de matemáticas listas.",
            "Esos son los últimos bloques. ¡Juega bien!",
            "La bolsa terminó. ¿Qué queda en tu mano?",
            "¡Bloques finales! La bandera está cerquita.",
            "¡Bolsa vacía! La torre depende de ti."
        ],
        _ =>
        [
            "¡Es tu turno!",
            "Tu movimiento, maestro constructor.",
            "¡Vamos, elige un bloque!",
            "La torre está lista para ti.",
            "¡Tu turno! ¿Qué vas a construir?",
            "Muéstrame tu mejor jugada.",
            "Estoy lista. ¿Y tú?",
            "¡A construir!",
            "Tu turno de brillar.",
            "Adelante, aquí espero."
        ]
    };
}

public sealed class MoonTowerLinePicker
{
    private readonly Dictionary<LunaLineEvent, Queue<int>> _recent = new();
    private readonly Random _rng;

    public MoonTowerLinePicker(int seed) => _rng = new Random(seed);

    public string Next(LunaLineEvent kind, string? language, int level = 0, int needed = 0)
    {
        var count = MoonTowerLunaLines.Count(kind);
        if (!_recent.TryGetValue(kind, out var recent))
        {
            recent = new Queue<int>();
            _recent[kind] = recent;
        }

        var keep = Math.Max(1, Math.Min(4, count - 1));
        while (recent.Count >= keep) recent.Dequeue();
        var candidates = Enumerable.Range(0, count).Where(i => !recent.Contains(i)).ToList();
        var pick = candidates.Count == 0 ? _rng.Next(count) : candidates[_rng.Next(candidates.Count)];
        recent.Enqueue(pick);
        return MoonTowerLunaLines.For(kind, language, pick, level, needed);
    }
}
