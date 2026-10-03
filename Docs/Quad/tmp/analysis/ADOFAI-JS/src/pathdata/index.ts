/** Standard direction characters → absolute angle */
const pathDataTable: Record<string, number> = {
  "R": 0, "p": 15, "J": 30, "E": 45, "T": 60, "o": 75, "U": 90, "q": 105,
  "G": 120, "Q": 135, "H": 150, "W": 165, "L": 180, "x": 195, "N": 210,
  "Z": 225, "F": 240, "V": 255, "D": 270, "Y": 285, "B": 300, "C": 315,
  "M": 330, "A": 345, "!": 999
};

/**
 * Special offset characters — these are NOT absolute angles.
 * Instead, they represent a relative change from the previous angle:
 *   result = previous_angle + offset
 */
const offsetMap: Record<string, number> = {
  "5": 72,
  "6": -72,
  "7": 52,
  "8": -52,
  "9": -30,
  "h": 120,
  "j": -120,
  "t": 60,
  "y": 300,
};

/**
 * charCode → 查找表（-1 表示无映射）。
 * 用 Int16Array 按 charCode 索引，避免解析热路径上的字符串哈希查找。
 */
const ANGLE_LUT = new Int16Array(128).fill(-1);
for (const key in pathDataTable) {
  const code = key.charCodeAt(0);
  if (code < 128) ANGLE_LUT[code] = pathDataTable[key];
}

const OFFSET_LUT = new Int16Array(128).fill(-1);
for (const key in offsetMap) {
  const code = key.charCodeAt(0);
  if (code < 128) OFFSET_LUT[code] = offsetMap[key];
}

const parseToangleData = (pathdata: string): number[] => {
  const result: number[] = new Array(pathdata.length);
  let prev = 0;

  for (let i = 0; i < pathdata.length; i++) {
    const c = pathdata.charCodeAt(i);

    if (c < 128) {
      const angle = ANGLE_LUT[c];
      if (angle !== -1) {
        result[i] = angle;
        prev = angle;
        continue;
      }
      const offset = OFFSET_LUT[c];
      if (offset !== -1) {
        result[i] = prev + offset;
        prev = result[i];
        continue;
      }
    }

    // Unknown character: keep current angle
    result[i] = prev;
  }

  return result;
};

export default {
    pathDataTable,
    parseToangleData
}
