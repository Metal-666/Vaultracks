using System.Reactive.Subjects;

namespace Vaultracks;

public static class EventBus {

	public static Subject<(UserAuth UserAuth, Location Location)> LocationEvents { get; } =
		new();

}