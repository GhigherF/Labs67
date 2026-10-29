const grpc = require("@grpc/grpc-js");
const protoLoader = require("@grpc/proto-loader");
const path = require("path");

const PROTO_PATH = path.join(__dirname, "calculator.proto");

const packageDefinition = protoLoader.loadSync(PROTO_PATH, {
  keepCase: true,
  longs: String,
  enums: String,
  defaults: true,
  oneofs: true
});

const proto = grpc.loadPackageDefinition(packageDefinition);
const calculator = proto.calculator;

function SUM(call, callback) {
  callback(null, {
    result: call.request.x + call.request.y
  });
}

function SUB(call, callback) {
  callback(null, {
    result: call.request.x - call.request.y
  });
}

function MUL(call, callback) {
  callback(null, {
    result: call.request.x * call.request.y
  });
}

function DIV(call, callback) {
  const x = call.request.x;
  const y = call.request.y;

  if (y === 0) {
    return callback({
      code: grpc.status.INVALID_ARGUMENT,
      message: "Division by zero is not allowed."
    });
  }

  callback(null, {
    result: x / y
  });
}

function FACT(call, callback) {
  const x = call.request.x;

  if (!Number.isInteger(x)) {
    return callback({
      code: grpc.status.INVALID_ARGUMENT,
      message: "Factorial argument must be an integer."
    });
  }

  if (x < 0) {
    return callback({
      code: grpc.status.INVALID_ARGUMENT,
      message: "Factorial is not defined for negative integers."
    });
  }

  if (x > 12) {
    return callback({
      code: grpc.status.OUT_OF_RANGE,
      message: "Factorial result exceeds Int32.MaxValue."
    });
  }

  let result = 1;

  for (let i = 2; i <= x; i++) {
    result *= i;
  }

  callback(null, { result });
}

const handlers = { SUM, SUB, MUL, DIV, FACT };
const serviceHandlers = {};

for (const methodName of Object.keys(calculator.Calculator.service)) {
  const handler = handlers[methodName.toUpperCase()];

  if (handler) {
    serviceHandlers[methodName] = handler;
  }
}

const server = new grpc.Server();

server.addService(
  calculator.Calculator.service,
  serviceHandlers
);

server.bindAsync(
  "0.0.0.0:5001",
  grpc.ServerCredentials.createInsecure(),
  (error, port) => {
    if (error) {
      console.error("Ошибка запуска сервера:", error.message);
      process.exitCode = 1;
      return;
    }

    console.log(`gRPCServer02 запущен на порту ${port}`);
    server.start();
  }
);
