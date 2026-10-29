package main

import (
	"context"
	"fmt"
	"time"

	pb "goClient/calculator"

	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/status"
)

func call(name string, operation func(context.Context) error) {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	err := operation(ctx)

	if err != nil {
		if s, ok := status.FromError(err); ok {
			fmt.Printf(
				"%s: ошибка gRPC [%s] — %s\n",
				name,
				s.Code(),
				s.Message(),
			)
		} else {
			fmt.Printf("%s: ошибка — %v\n", name, err)
		}
	}
}

func main() {
	conn, err := grpc.NewClient(
		"localhost:5001",
		grpc.WithTransportCredentials(insecure.NewCredentials()),
	)
	if err != nil {
		fmt.Println("Ошибка подключения:", err)
		return
	}
	defer conn.Close()

	client := pb.NewCalculatorClient(conn)

	call("SUM", func(ctx context.Context) error {
		res, err := client.SUM(ctx, &pb.BinaryRequest{
			X: 10,
			Y: 5,
		})
		if err != nil {
			return err
		}
		fmt.Printf("SUM(10, 5) = %v\n", res.GetResult())
		return nil
	})

	call("SUB", func(ctx context.Context) error {
		res, err := client.SUB(ctx, &pb.BinaryRequest{
			X: 10,
			Y: 5,
		})
		if err != nil {
			return err
		}
		fmt.Printf("SUB(10, 5) = %v\n", res.GetResult())
		return nil
	})

	call("MUL", func(ctx context.Context) error {
		res, err := client.MUL(ctx, &pb.BinaryRequest{
			X: 10,
			Y: 5,
		})
		if err != nil {
			return err
		}
		fmt.Printf("MUL(10, 5) = %v\n", res.GetResult())
		return nil
	})

	call("DIV", func(ctx context.Context) error {
		res, err := client.DIV(ctx, &pb.BinaryRequest{
			X: 10,
			Y: 5,
		})
		if err != nil {
			return err
		}
		fmt.Printf("DIV(10, 5) = %v\n", res.GetResult())
		return nil
	})

	call("FACT", func(ctx context.Context) error {
		res, err := client.FACT(ctx, &pb.FactorialRequest{
			X: 5,
		})
		if err != nil {
			return err
		}
		fmt.Printf("FACT(5) = %d\n", res.GetResult())
		return nil
	})

	fmt.Println("\nПроверка исключений:")

	call("DIV", func(ctx context.Context) error {
		res, err := client.DIV(ctx, &pb.BinaryRequest{
			X: 10,
			Y: 0,
		})
		if err != nil {
			return err
		}
		fmt.Printf("DIV(10, 0) = %v\n", res.GetResult())
		return nil
	})

	call("FACT", func(ctx context.Context) error {
		res, err := client.FACT(ctx, &pb.FactorialRequest{
			X: 13,
		})
		if err != nil {
			return err
		}
		fmt.Printf("FACT(13) = %d\n", res.GetResult())
		return nil
	})

	call("FACT", func(ctx context.Context) error {
		res, err := client.FACT(ctx, &pb.FactorialRequest{
			X: -5,
		})
		if err != nil {
			return err
		}
		fmt.Printf("FACT(-5) = %d\n", res.GetResult())
		return nil
	})

	_ = codes.OK
	fmt.Println("\nВсе вызовы завершены.")
}
