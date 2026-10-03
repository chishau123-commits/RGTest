/**
    * @param {object} obj - JSON Object
    * @param {number} indentLevel - 当前缩进层级（数字，乘以 indentStep 得到实际缩进）
    * @param {boolean} isRoot - Is JSON the Root?
    * @param {string} indentChar - 单次缩进使用的字符，默认制表符 '\t'
    * @param {number} indentStep - 每层增加的缩进数（默认 1）
    * @returns 7bgson
*/


function exportAsADOFAI(
  obj: any,
  indentLevel: number = 0,
  isRoot: boolean = false,
  indentChar: string = '\t',
  indentStep: number = 1
): string {

  if (typeof obj !== 'object' || obj === null) {
    return JSON.stringify(obj);
  }
  
  if (Array.isArray(obj)) {
    const allPrimitives = obj.every(item => 
      typeof item !== 'object' || item === null
    );
    
    if (allPrimitives) {
      return '[' + obj.map(item => exportAsADOFAI(item, 0, false, indentChar, indentStep)).join(',') + ']';
    }
    
    const spaces = indentChar.repeat(indentLevel);
    const itemIndent = indentChar.repeat(indentLevel + indentStep);
    const arrayItems = obj.map(item => 
      itemIndent + formatAsSingleLine(item, indentChar)
    ).join(',\n');
    
    return '[\n' + arrayItems + '\n' + spaces + ']';
  }
  
  const spaces = indentChar.repeat(indentLevel);
  const keys = Object.keys(obj);
  
  if (isRoot) {
    const childIndent = indentChar.repeat(indentStep);
    const objectItems = keys.map(key => 
      childIndent + JSON.stringify(key) + ': ' + exportAsADOFAI((obj as Record<string, any>)[key], indentStep, false, indentChar, indentStep)
    ).join(',\n');
    
    return '{\n' + objectItems + '\n}';
  }
  
  const objectItems = keys.map(key => 
    spaces + indentChar.repeat(indentStep) + JSON.stringify(key) + ': ' + exportAsADOFAI((obj as Record<string, any>)[key], indentLevel + indentStep, false, indentChar, indentStep)
  ).join(',\n');
  
  return '{\n' + objectItems + '\n' + spaces + '}';
}

/**
    * @param {any} obj Eventlist to keep
    * @param {string} indentChar 缩进字符，传递给内部调用
    * @returns {string} JSON formated as singleline
*/
function formatAsSingleLine(obj: any, indentChar: string = '\t'): string {
  if (typeof obj !== 'object' || obj === null) {
    return exportAsADOFAI(obj, 0, false, indentChar);
  }
  
  if (Array.isArray(obj)) {
    return '[' + obj.map(item => formatAsSingleLine(item, indentChar)).join(',') + ']';
  }
  
  const keys = Object.keys(obj);
  const entries = keys.map(key => 
    JSON.stringify(key) + ': ' + formatAsSingleLine((obj as Record<string, any>)[key], indentChar)
  ).join(', ');
  
  return '{' + entries + '}';
}

export default exportAsADOFAI;