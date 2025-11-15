const mathResult = scriptbox.math.add(5, 7);
console.log('math.add =>', mathResult);

scriptbox.files.writeAll('demo/result.txt', `sum=${mathResult}`);
const fileContents = scriptbox.files.readAll('demo/result.txt');
console.log('files.readAll =>', fileContents);
