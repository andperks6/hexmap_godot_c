class_name HexDirectionsClass

#region Enumerations

enum HexDirections { NE, E, SE, SW, W, NW }

#endregion

#region Statc methods

static func opposite (direction: HexDirections) -> HexDirections:
	if int(direction) < 3:
		return int(direction) + 3 as HexDirectionsClass.HexDirections
	else:
		return int(direction) - 3 as HexDirectionsClass.HexDirections

static func previous (direction: HexDirections) -> HexDirections:
	if (direction == HexDirections.NE):
		return HexDirections.NW
	else:
		return direction - 1 as HexDirectionsClass.HexDirections

static func next (direction: HexDirections) -> HexDirections:
	if (direction == HexDirections.NW):
		return HexDirections.NE
	else:
		return direction + 1 as HexDirectionsClass.HexDirections

static func previous2 (direction: HexDirections) -> HexDirections:
	direction -= 2
	if (direction >= HexDirections.NE):
		return direction
	else:
		return (direction + 6) as HexDirectionsClass.HexDirections

static func next2 (direction: HexDirections) -> HexDirections:
	direction += 2
	if (direction <= HexDirections.NW):
		return direction
	else:
		return (direction - 6) as HexDirectionsClass.HexDirections

#endregion
