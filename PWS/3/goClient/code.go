package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"time"
)

const serverURL = "http://localhost:5000/rpc"

type Request struct {
	JSONRPC string `json:"jsonrpc"`
	Method  string `json:"method"`
	Params  any    `json:"params,omitempty"`
	ID      any    `json:"id,omitempty"`
}

type Response struct {
	JSONRPC string          `json:"jsonrpc"`
	Result  json.RawMessage `json:"result"`
	Error   *RPCError       `json:"error"`
	ID      json.RawMessage `json:"id"`
}

type RPCError struct {
	Code    int    `json:"code"`
	Message string `json:"message"`
}

var client = &http.Client{
	Timeout: 5 * time.Second,
}

func call(method string, params any, id int) {
	req := Request{
		JSONRPC: "2.0",
		Method:  method,
		Params:  params,
		ID:      id,
	}

	data, err := json.Marshal(req)
	if err != nil {
		fmt.Println("Ошибка создания запроса:", err)
		return
	}

	httpResponse, err := client.Post(
		serverURL,
		"application/json",
		bytes.NewReader(data),
	)
	if err != nil {
		fmt.Println("Ошибка HTTP:", err)
		return
	}
	defer httpResponse.Body.Close()

	body, err := io.ReadAll(httpResponse.Body)
	if err != nil {
		fmt.Println("Ошибка чтения ответа:", err)
		return
	}

	if httpResponse.StatusCode == http.StatusNoContent {
		fmt.Printf("%s: уведомление обработано без ответа\n", method)
		return
	}

	var response Response
	if err := json.Unmarshal(body, &response); err != nil {
		fmt.Println("Ошибка разбора ответа:", err)
		return
	}

	if response.Error != nil {
		fmt.Printf(
			"%s: ошибка JSON-RPC [%d] — %s\n",
			method,
			response.Error.Code,
			response.Error.Message,
		)
		return
	}

	fmt.Printf("%s: результат = %s\n", method, response.Result)
}

func batchCall() {
	requests := []Request{
		{
			JSONRPC: "2.0",
			Method:  "SUM",
			Params:  []any{10, 5},
			ID:      101,
		},
		{
			JSONRPC: "2.0",
			Method:  "SUB",
			Params: map[string]any{
				"x": 10,
				"y": 5,
			},
			ID: 102,
		},
		{
			JSONRPC: "2.0",
			Method:  "MUL",
			Params:  []any{10, 5},
			ID:      103,
		},
		{
			JSONRPC: "2.0",
			Method:  "DIV",
			Params: map[string]any{
				"x": 10,
				"y": 0,
			},
			ID: 104,
		},
		{
			JSONRPC: "2.0",
			Method:  "FACT",
			Params: map[string]any{
				"x": 5,
			},
			ID: 105,
		},
		{
			JSONRPC: "2.0",
			Method:  "FACT",
			Params:  []any{13},
			ID:      106,
		},
	}

	data, err := json.Marshal(requests)
	if err != nil {
		fmt.Println("Ошибка создания пакета:", err)
		return
	}

	httpResponse, err := client.Post(
		serverURL,
		"application/json",
		bytes.NewReader(data),
	)
	if err != nil {
		fmt.Println("Ошибка HTTP:", err)
		return
	}
	defer httpResponse.Body.Close()

	body, err := io.ReadAll(httpResponse.Body)
	if err != nil {
		fmt.Println("Ошибка чтения пакета:", err)
		return
	}

	if httpResponse.StatusCode != http.StatusOK {
		fmt.Printf("HTTP ошибка: %s\n", httpResponse.Status)
		fmt.Println(string(body))
		return
	}

	var responses []Response
	if err := json.Unmarshal(body, &responses); err != nil {
		fmt.Println("Ошибка разбора пакета:", err)
		return
	}

	fmt.Println("Результаты пакетного вызова:")

	for _, response := range responses {
		if response.Error != nil {
			fmt.Printf(
				"id=%s: ошибка [%d] — %s\n",
				response.ID,
				response.Error.Code,
				response.Error.Message,
			)
		} else {
			fmt.Printf(
				"id=%s: результат = %s\n",
				response.ID,
				response.Result,
			)
		}
	}
}

func main() {
	fmt.Println("Одиночные вызовы:")

	call("SUM", []any{10, 5}, 1)
	call("SUB", map[string]any{"x": 10, "y": 5}, 2)
	call("MUL", []any{10, 5}, 3)
	call("DIV", map[string]any{"x": 10, "y": 5}, 4)
	call("FACT", []any{5}, 5)

	fmt.Println("\nПроверка ошибок:")

	call("DIV", []any{10, 0}, 6)
	call("FACT", map[string]any{"x": 13}, 7)
	call("UNKNOWN", []any{}, 8)

	fmt.Println()
	batchCall()
}
